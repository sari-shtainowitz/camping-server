# Container image for the Camping API - used to deploy to Render (or any Docker host).
# Render has no native .NET runtime, so it builds and runs this image.

# ---- build ---------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Restore first (cached as long as the project files don't change).
COPY Camping.Core/Camping.Core.csproj Camping.Core/
COPY Camping.Data/Camping.Data.csproj Camping.Data/
COPY Camping.Service/Camping.Service.csproj Camping.Service/
COPY Camping.API/Camping.API.csproj Camping.API/
RUN dotnet restore Camping.API/Camping.API.csproj

COPY Camping.Core/ Camping.Core/
COPY Camping.Data/ Camping.Data/
COPY Camping.Service/ Camping.Service/
COPY Camping.API/ Camping.API/
RUN dotnet publish Camping.API/Camping.API.csproj -c Release -o /app --no-restore

# ---- run -----------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build /app .

# Render tells the app which port to listen on through $PORT (8080 elsewhere).
# Secrets are NOT in the image: set ConnectionStrings__CampingDb and Jwt__Key as
# environment variables on the host. Migrations run on start-up.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet Camping.API.dll"]
