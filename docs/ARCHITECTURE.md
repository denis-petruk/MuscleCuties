# Architecture overview

MuscleCuties has a .NET MAUI mobile client and a first-party ASP.NET Core API. The code is organized so that domain rules stay independent of UI, hosting, and storage details.

| Project | Responsibility |
| --- | --- |
| `src/MuscleCuties.Core` | Domain entities and rules |
| `src/MuscleCuties.UseCases` | Application workflows and data contracts |
| `src/MuscleCuties.Infrastructure` | Client-side service and storage adapters |
| `src/MuscleCuties.Infrastructure.PostgreSql` | PostgreSQL persistence adapters and migrations |
| `src/MuscleCuties.UI` | MAUI screens and view models |
| `services/MuscleCuties.Server` | HTTP API and AWS Lambda host |

The mobile client calls application workflows through plain input and output contracts. The server routes requests into the same domain and use-case layers, with PostgreSQL mapping kept in its infrastructure project. The Core project does not depend on MAUI, ASP.NET Core, or Entity Framework.

The app stores offline snapshots in its local cache. Account data is handled by the first-party backend. The test projects cover domain rules, mobile and server contracts, and persistence against disposable PostgreSQL databases.
