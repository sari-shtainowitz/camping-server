using System.Text;
using Camping.API.Authentication;
using Camping.API.Json;
using Camping.API.Mapping;
using Camping.API.Middleware;
using Camping.Core.Interfaces;
using Camping.Data;
using Camping.Data.Repositories;
using Camping.Service.Interfaces;
using Camping.Service.Security;
using Camping.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Step 13): NLog is the only logging provider; it loads nlog.config. ---
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Add services to the container.

// The connection string holds the database password, so it is not in
// appsettings.json: User Secrets in development, an environment variable
// (ConnectionStrings__CampingDb) anywhere else.
var connectionString = builder.Configuration.GetConnectionString("CampingDb");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:CampingDb is not configured. For local development run: " +
        "dotnet user-secrets set \"ConnectionStrings:CampingDb\" " +
        "\"Host=localhost;Port=5432;Database=CampingDb;Username=postgres;Password=<your password>\" --project Camping.API");
}

builder.Services.AddDbContext<Camping.Data.DbContext>(options =>
    options.UseNpgsql(connectionString));

// AutoMapper: register the API mapping profile(s).
builder.Services.AddAutoMapper(typeof(MappingProfile));

// --- Repositories (Step 5) ---
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICampingSiteRepository, CampingSiteRepository>();
builder.Services.AddScoped<ITentRepository, TentRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IAmenityRepository, AmenityRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();

// --- Services (Step 7 / Step 8) ---
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICampingSiteService, CampingSiteService>();
builder.Services.AddScoped<ITentService, TentService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IAmenityService, AmenityService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// --- Authentication (Step 8) ---
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

// The signing key is a secret, so it is NOT in appsettings.json (which is in the
// repo). In development it comes from User Secrets, in production from the
// Jwt__Key environment variable. Fail fast with instructions if it is missing.
if (string.IsNullOrWhiteSpace(jwtSettings.Key) || Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is not configured (it must be at least 32 bytes). For local development run: " +
        "dotnet user-secrets set \"Jwt:Key\" \"<a long random string>\" --project Camping.API");
}

builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // .NET 6's System.Text.Json cannot (de)serialize DateOnly on its own.
        options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // DateOnly has no default OpenAPI mapping on .NET 6 - describe it as a date string.
    options.MapType<DateOnly>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "date",
        Example = new OpenApiString("2025-07-01")
    });

    // Surface the /// summaries on controller actions in the Swagger UI.
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT returned by /api/auth/login.",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

var app = builder.Build();

// --- Database: apply pending migrations on startup -----------------------------
// So the system runs locally with no manual database step: an empty PostgreSQL
// gets the schema and the seed data (demo users, tents) the first time the API
// starts. Can be switched off with "Database:MigrateOnStartup": false.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<Camping.Data.DbContext>();
    app.Logger.LogInformation("Applying database migrations (if any are pending)");
    await db.Database.MigrateAsync();
}

// --- Middleware pipeline (Step 12 / Step 13) ---
// 1. Exception handler first, so it wraps everything downstream - including the
//    correlation-id and logging middleware, routing, authentication, model
//    binding and the controllers - turning any failure into a consistent
//    problem+json response and logging it (Step 13). The correlation id set by
//    the next middleware is stored in HttpContext.Items, so the handler can still
//    read it when an exception bubbles back up.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. Correlation id next: every component below (and every log line) can read
//    and echo the same id.
app.UseMiddleware<CorrelationIdMiddleware>();

// 3. Request logger: one Information line per incoming request, carrying the
//    correlation id (Step 13).
app.UseMiddleware<RequestLoggingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
