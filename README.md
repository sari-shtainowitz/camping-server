# Camping Reservation System

A camping-site reservation API (ASP.NET Core 6 / EF Core 6 / PostgreSQL) plus a
small React client that proves it works end to end. Guests browse the tents of a
lakeside camping site, check availability for their dates, pay with a mock card
and get a booking confirmation; admins manage the tent catalogue and see the
reservation schedule. Guests can also leave public reviews.

**The React client lives in its own repository:**
[sari-shtainowitz/camping-client](https://github.com/sari-shtainowitz/camping-client)
(clone it next to this one, or into a `Client/` folder here - that folder is git-ignored).

---

## The limited resource: a tent

A tent can be booked over time but **never for overlapping dates**. The race the
system must survive: *two guests book the same tent for the same night at the
same moment, both pass the "is it free?" check, and both would get a
confirmation - which must never happen.*

How the server handles it (`Camping.Data/Repositories/ReservationRepository.cs`):

1. **Optimistic concurrency token** - `Tent.RowVersion` (`[ConcurrencyCheck]`,
   a `bytea` column, since PostgreSQL has no `rowversion`). `DbContext.SaveChanges`
   gives the tent a new token whenever a reservation for it is added or changed,
   and EF puts the old value in the `WHERE` of the `UPDATE`.
2. **Check and save in one transaction** - the availability query
   (`HasOverlapAsync`, one `EXISTS` query) and the insert run inside the same
   transaction.
3. When two requests race, the second `UPDATE` matches zero rows, EF throws
   `DbUpdateConcurrencyException`, the repository catches **exactly that
   exception**, and the API answers **409 Conflict** with a clear message.
   A booking for dates that are already taken is also **409** (it conflicts with
   the current state of the tent). Both are logged as **Warning**.

The proof is `TentConcurrencyTests`: two separate `DbContext`s read the same tent,
both change it, the first save wins and the second gets
`DbUpdateConcurrencyException`. The client can demonstrate the 409 too (see below).

---

## Repository layout

```
├── Camping.sln
├── Camping.Core/      entities, enums, ALL interfaces (repositories, services,
│                      password hasher), DTOs, query/paging types           - references nothing
├── Camping.Data/      EF Core DbContext, migrations, repository implementations -> Core
├── Camping.Service/   service implementations (business rules), PBKDF2 hasher   -> Core
├── Camping.API/       controllers, AutoMapper, JWT, middleware, NLog         -> Service, Core (+ Data for DI only)
├── Camping.Tests/     xUnit + Moq unit tests, PostgreSQL integration tests
└── (Client/)          the React client - a separate repository, git-ignored here
```

---

## Run it locally

### 1. Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 6.0.x or newer (the projects target `net6.0`) |
| PostgreSQL | any local instance on `localhost:5432` (13+) |
| Node.js | 18+ (client only) |

### 2. Secrets (User Secrets - never in the repo)

`appsettings.json` contains **no** password and **no** JWT key. Set them once:

```
dotnet user-secrets set "ConnectionStrings:CampingDb" "Host=localhost;Port=5432;Database=CampingDb;Username=postgres;Password=<your-password>" --project Camping.API
dotnet user-secrets set "Jwt:Key" "<any random string of 32+ characters>" --project Camping.API
```

Without the connection string the API refuses to start and prints this exact
command. Without `Jwt:Key`, Development still starts with a temporary random
key (a Warning is logged; tokens stop working after a restart); every other
environment refuses to start. Outside development use the environment variables
`ConnectionStrings__CampingDb` and `Jwt__Key`.

### 3. Start the API

```
dotnet run --project Camping.API --launch-profile Camping.API
```

On startup the API **applies the EF Core migrations itself** - an empty database
gets the schema and the seed data with no manual step
(`Database:MigrateOnStartup`, default `true`). Migrations in the history:
`InitialCreate`, `AddReviews`.

* API: `https://localhost:7195` (and `http://localhost:5035`)
* Swagger UI (Development): `https://localhost:7195/swagger`

### 4. Start the client

```
cd Client
npm install
npm run dev        # http://localhost:5173
```

Vite proxies `/api/*` to `https://localhost:7195` (override with
`VITE_API_PROXY_TARGET`), so the server needs no CORS configuration.

### Demo users (seed data)

| Role | Email | Password |
|------|-------|----------|
| Customer | `customer@camping.test` | `Customer123!` |
| Admin | `admin@camping.test` | `Admin123!` |

Register in the client to get more Customers.

---

## API overview

| Resource | Endpoints | Who |
|----------|-----------|-----|
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` | anyone / signed in |
| Tents | `GET /api/tents` (paged, filter: type, minCapacity, price range, isActive; sort), `GET /api/tents/{id}`, `POST`, `PUT`, `DELETE` | read: signed in, write: Admin |
| Tent occupancy | `GET /api/tents/occupancy?from=&to=` - which tents are booked on which dates, **for all guests**, with no personal data | signed in |
| Camping sites, Amenities | same CRUD shape (`/api/camping-sites`, `/api/amenities`); a camping site is returned with its amenities (many-to-many) | read: signed in, write: Admin |
| Reservations | `GET /api/reservations` (paged), `GET /{id}`, `POST`, `PUT /{id}`, `DELETE /{id}` | Customer books; owners or Admin read/change |
| Users | `GET /api/users`, `GET /{id}`, `PUT /{id}`, `DELETE /{id}` | Admin |
| Reviews | `GET /api/reviews` (public), `POST` (signed in), `DELETE /{id}` (Admin) | |

Status codes: `200`, `201` + `Location`, `204`, `400` (validation / business
rule), `401`, `403`, `404`, `409` (tent conflict). Every error has the same
problem-JSON shape (`title`, `status`, `detail`, `instance`) with the request's `correlationId`.

Authorization is enforced **on the server**: a Customer always books as the user
in their JWT (a `userId` in the body is ignored), only sees their own
reservations, and gets `403` on anyone else's.

**Privacy vs. availability:** a guest's reservations (who, price, id) are
private - `GET /api/reservations` returns only the caller's own. Availability is
shared: `GET /api/tents/occupancy` returns just *tent + dates* of every active
booking, so a tent one guest booked shows as taken for everyone in the client's
tent list and map, before they try to pay.

**Validation, two levels:**
* *Shape* - Data Annotations on the request DTOs (`[Required]`, `[Range]`,
  `[StringLength]`...). `[ApiController]` checks `ModelState` before the action
  runs; an invalid model gets a `400` built by `InvalidModelStateResponseFactory`
  (`Program.cs`) in the same problem+json shape, with an `errors` map per field.
* *Business rules* that depend on the data (dates, tent active, tent free,
  unique email/name) live in the services, never in the controllers.

---

## Data access (EF Core)

* Code-First, a `DbSet` per entity, Fluent API configuration in `DbContext`.
* Relationships: one-to-many (camping site -> tents, tent -> reservations,
  user -> reservations/reviews) and many-to-many (camping site <-> amenities).
* `Include` / `ThenInclude` load related data in the same query - a reservation
  comes with its tent, the tent's camping site and the guest; a camping site
  with its amenities. No per-row follow-up queries (no N+1).
* Paging is `Skip`/`Take` inside the SQL query; filters and sorting are LINQ
  translated to SQL; every read-only query uses `AsNoTracking`.
* Fully async from controller to database, with the request's
  `CancellationToken` passed through every layer.

## Dependency injection lifetimes

Registered in `Program.cs`, all through constructor injection:

| Lifetime | What | Why |
|----------|------|-----|
| Scoped | `DbContext`, repositories, services | one instance per HTTP request; a `DbContext` tracks entities for one unit of work and is not thread-safe |
| Singleton | `Pbkdf2PasswordHasher`, `JwtTokenService` | stateless, safe to share |

A **singleton that holds a scoped service is a bug** ("captive dependency"):
the singleton lives for the whole app, so the scoped object it captured (e.g. a
`DbContext`) would be shared by every request at once - stale tracked data,
concurrent use of a non-thread-safe context, and a connection that is never
released. That is why nothing registered as a singleton depends on a repository
or the `DbContext`.

---

## Logging (NLog)

`nlog.config`; the code only uses .NET's `ILogger`.

* every incoming request - `Information`, with the CorrelationId
  (`CorrelationIdMiddleware`, also returned in the `X-Correlation-ID` header)
* normal flow (sign-in, reservation created / deleted, review posted) - `Information`
* availability checks - `Debug` (shown in Development only: `NLog:AppMinLevel`)
* a conflict over a tent - `Warning`
* any other handled exception - `Error`

Passwords, tokens and registration payloads are never logged (only user ids).
Logs go to the console and `Camping.API/bin/.../logs/`.

---

## Tests

```
dotnet test
```

**25 tests:**

* `ReservationServiceTests` (10) - xUnit + Moq, service layer with mocked
  repositories: a valid booking, every rejection reason, an already-booked tent
  (409), a free tent, a lost concurrency race, and that the availability check
  runs inside the repository's transaction.
* `ReservationsControllerAuthorizationTests` (9) - the real controller with a
  JWT-like `ClaimsPrincipal`: ownership (403), Admin access, a Customer cannot
  list, book or move a reservation as another user.
* `TentOccupancyTests` (3) - the shared occupancy view: the requested window, the
  default window (today .. one year), and rejecting a window that ends before it starts.
* `TentConcurrencyTests` (3) - against real PostgreSQL: two `DbContext`s, the
  second save gets `DbUpdateConcurrencyException`; a booking rotates the tent's
  token; the date-overlap query's boundaries.

The integration tests use a **separate** database, `CampingDb_ConcurrencyTests`,
created and dropped on every run. They take the server and password from the
same User Secret (`ConnectionStrings:CampingDb`) with the database name swapped,
or from the `CAMPING_TEST_DB` environment variable.

---

## Client ([camping-client](https://github.com/sari-shtainowitz/camping-client))

Only a demonstration of the server:

1. Login / registration - JWT saved to `localStorage`, sent as `Authorization: Bearer`.
2. Tent list with **real server-side pagination and filters** (`/api/tents?page=…&pageSize=3&type=…`).
3. Booking the limited resource through a mock card checkout (15-minute timer;
   the reservation is sent only after the mock payment is approved; test card
   `4242 4242 4242 4242`).
4. **409 handling** - a taken tent shows a clear message. To demonstrate it, tick
   *"הדגמת התנגשות (409)"* on the checkout page: the same booking is sent twice at
   once, the server accepts one and rejects the other with 409.
5. Public reviews page.
