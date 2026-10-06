# Camping Reservation System

A camping-site reservation API (ASP.NET Core 6 / EF Core 6 / PostgreSQL) plus a
small React client that proves it works end to end.

The **limited resource** is a **tent**: it can be booked over time but never
double-booked for overlapping dates. Concurrent bookings are guarded by an
optimistic-concurrency token on the tent row.

---

## Repository layout

```
CampingProject/                 (this folder)
├── Camping.sln                  .NET solution (5 projects)
├── Camping.Core/                entities, enums, interfaces, query/paging types
├── Camping.Data/                EF Core DbContext, migrations, repositories
├── Camping.Service/             business rules (services), password hashing
├── Camping.API/                 controllers, DTOs, AutoMapper, JWT auth, middleware, NLog
├── Camping.Tests/               xUnit tests (unit + integration)
└── Client/                      React + TypeScript client (Vite) - NOT part of Camping.sln
```

The client is deliberately outside the solution so client and server can be
versioned and deployed separately.

---

## Prerequisites

| Tool | Version used |
|------|--------------|
| .NET SDK | 6.0.x |
| PostgreSQL | any local instance reachable at `localhost:5432` (PostgreSQL 13+ is fine) |
| EF Core tools | `dotnet tool install --global dotnet-ef --version 6.*` |
| Node.js | 18+ (only for the client) |

PostgreSQL is reached with username/password authentication (there is no
Windows-integrated-auth equivalent). You'll need a Postgres user (the default
`postgres` superuser works for local dev) and its password.

---

## Database

Connection string — `Camping.API/appsettings.json`:

```
Host=localhost;Port=5432;Database=CampingDb;Username=postgres;Password=<your-password>
```

Replace `<your-password>` with your local Postgres user's password (and
`Username=`/`Host=`/`Port=` if your instance differs). The `CampingDb` database
itself does not need to be created by hand — `dotnet ef database update` (below)
creates it if it doesn't already exist.

### Create the database from scratch

```
dotnet ef database update --project Camping.Data --startup-project Camping.API
```

This applies the single migration `20261006215930_InitialCreate`, which creates
all tables and inserts the seed data:

* 4 amenities, 1 camping site (with amenity links), 5 tents, 1 sample reservation
* 2 **demo users** (see below)

The test suite uses a **separate** database (`CampingDb_ConcurrencyTests`) that the
test fixture creates and drops on every run — it never touches `CampingDb`.

---

## Run the API

```
dotnet run --project Camping.API
```

Default URLs (from `Camping.API/Properties/launchSettings.json`):

* `https://localhost:7195`
* `http://localhost:5035`

Swagger UI (Development only): `https://localhost:7195/swagger`.

To run HTTP-only (handy for the client proxy):

```
dotnet run --project Camping.API --no-launch-profile
# then it listens on http://localhost:5000 / 5001, or set:
#   ASPNETCORE_URLS=http://localhost:5199 dotnet run --project Camping.API --no-launch-profile
```

---

## Authentication

JWT bearer tokens (HMAC-SHA256). Passwords are stored only as PBKDF2-SHA256
hashes (`Camping.Service/Security/Pbkdf2PasswordHasher.cs`).

| Endpoint | Purpose |
|----------|---------|
| `POST /api/auth/register` | create a **Customer** account, returns a token |
| `POST /api/auth/login` | exchange email + password for a token |
| `GET  /api/auth/me` | claims of the caller (requires a token) |
| `GET  /api/auth/admin-area` | demo endpoint, Admin role only |

Send the token on every other call:

```
Authorization: Bearer <token>
```

### Roles

| Role | Value | Can |
|------|-------|-----|
| `Customer` | 0 | read catalogues; **create** and manage reservations (book tents) |
| `Admin` | 1 | read catalogues; create/update/delete tents, camping sites, amenities; manage users; view the reservation schedule. Admins **cannot** create reservations |

Reads require any authenticated user. Missing/invalid token → **401**; wrong role → **403**.
Booking (`POST /api/reservations`) is Customer-only.

### Demo users

Seeded by the initial migration — usable immediately after `database update`:

| Role | Email | Password |
|------|-------|----------|
| Customer | `customer@camping.test` | `Customer123!` |
| Admin | `admin@camping.test` | `Admin123!` |

Register a new account to get another Customer. There is no self-service way to
create an Admin — sign in as the demo Admin and use
`PUT /api/users/{id}` to change another user's role.

> These are development credentials. Change the seed hashes (and the JWT signing
> key, next section) before any real deployment.

### Secrets

* `Jwt:Key` in `appsettings.json` is the literal placeholder
  `dev-signing-key-please-override-in-production-…` — not a real secret.
* `ConnectionStrings:CampingDb` **does** contain a real local Postgres password
  for development convenience. This is a local-dev shortcut, not a recommended
  practice: before sharing or deploying this project, move it into
  `dotnet user-secrets`, an environment variable, or a secrets manager instead
  of leaving it in `appsettings.json`.

For a real deployment, override `Jwt:Key` and `ConnectionStrings:CampingDb` with
environment variables, `dotnet user-secrets`, or a secrets manager — do **not**
commit real values. `.gitignore` already excludes `appsettings.*.local.json`,
`secrets.json`, and `.env*`.

---

## Testing

```
dotnet test
```

**19 tests, all green:**

* `ReservationServiceTests` (10) — xUnit + Moq, service layer in isolation:
  successful booking, every rejection reason, and the limited-resource
  concurrency behaviour.
* `ReservationsControllerAuthorizationTests` (7) — xUnit + Moq against the real
  `ReservationsController` with a `ClaimsPrincipal` standing in for a validated
  JWT: a Customer can read/the own reservations but gets 403 on another user's;
  an Admin can read/delete any reservation; a Customer's own `userId` filter
  cannot be overridden to list someone else's reservations.
* `TentConcurrencyTests` (2) — integration tests against a real PostgreSQL
  instance (`CampingDb_ConcurrencyTests`, auto-created/dropped): a stale second
  `DbContext` gets `DbUpdateConcurrencyException`; a successful booking rotates
  the tent's `RowVersion`.

If PostgreSQL is unreachable the 2 integration tests fail fast with one clear
message (no container runtime is used).

---

## Client (`Client/`)

React + TypeScript (Vite). Four features, only as proof the server works:

1. Login / registration — JWT saved to `localStorage`, sent as `Authorization: Bearer`
2. Tent list with **real server-side pagination**
3. Book a tent (the limited resource)
4. Clear message when the server returns **409 Conflict**

### Run

```
# 1. API (listens on https://localhost:7195 + http://localhost:5035)
dotnet run --project Camping.API

# 2. client
cd Client
npm install
npm run dev            # http://localhost:5173
```

Vite proxies `/api/*` to `https://localhost:7195` by default (`vite.config.ts`,
`secure: false` accepts the dev certificate), so the browser stays same-origin
and the server needs no CORS configuration. If you start the API on a different
URL (e.g. `ASPNETCORE_URLS=http://localhost:5199 dotnet run --project Camping.API
--no-launch-profile`), set `VITE_API_PROXY_TARGET` to match. Log in with a demo
user above.

### Build

```
cd Client
npm run build          # tsc -b && vite build  ->  Client/dist/
```

See `Client/README.md` for client-only detail.

---

## Requirements coverage (Steps 1–16)

| Step | Requirement | Where |
|------|-------------|-------|
| 1–4  | Layered solution, entities & relationships, EF Core + PostgreSQL, migrations from scratch | `Camping.*`, `Camping.Data/Migrations` |
| 5    | Repository layer, async, `AsNoTracking` for reads | `Camping.Data/Repositories` |
| 6    | Pagination / filtering / sorting on the database | `QueryableExtensions`, `*QueryParameters`, repositories |
| 7    | Business logic in services, not controllers | `Camping.Service/Services` |
| 8    | JWT auth, claims, roles, `[Authorize]` | `Camping.API/Authentication`, `Program.cs`, controllers |
| 9–10 | DTOs + AutoMapper (entities never cross the API), thin controllers, verbs, status codes, Swagger | `Camping.API/DTOs`, `Mapping`, `Controllers` |
| 11   | Optimistic concurrency on the tent (`RowVersion`), transactional check+save, `DbUpdateConcurrencyException` → `ConcurrencyConflictException` → **409** | `DbContext.SaveChanges`, `ReservationRepository`, `ReservationsController` |
| 12   | CorrelationId + global exception middleware, consistent `application/problem+json` errors, correct pipeline order | `Camping.API/Middleware`, `Program.cs` |
| 13   | NLog: request logging with CorrelationId, handled exceptions as Error, concurrency conflicts as Warning, no passwords/tokens logged | `nlog.config`, `RequestLoggingMiddleware`, `ExceptionHandlingMiddleware` |
| 14   | xUnit + Moq unit tests for the service layer (success, rejection, concurrency) | `Camping.Tests/ReservationServiceTests.cs` |
| 15   | React client: auth + JWT, list with server pagination, booking, 409 handling | `Client/` |
| 16   | README, demo users per role, `.gitignore`, no secrets, clean-database start, this checklist | this file, `.gitignore`, seed migration |
