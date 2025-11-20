# eShopOnWeb

This solution is organized as a multi-project ASP.NET Core application inside the `src/` folder. The following subprojects make up the core of the system:

- `ApplicationCore`: Domain entities, value objects, DTOs, and the interfaces that describe the business logic contracts. Other projects depend on these abstractions to keep the domain isolated.
- `Infrastructure`: Implementations of the `ApplicationCore` interfaces. Includes HTTP clients, caching, authentication helpers, RabbitMQ integrations, and data access concerns that talk to external services.
- `Web`: The primary ASP.NET Core MVC application that customers interact with. It wires up dependency injection, hosts background services, exposes controllers/pages, and serves the Blazor admin client.
- `BlazorAdmin`: A server-side Blazor application that provides an administrative UI for catalog management. It consumes the same APIs as the public site but focuses on CRUD workflows for admins.
- `BlazorShared`: Shared models, configuration objects, and client-facing DTOs that are reused by both the `Web` and `BlazorAdmin` projects to avoid duplication.

Tests live under `tests/`, and additional tooling such as infrastructure-as-code templates can be found in other top-level folders. Start by restoring dependencies (`dotnet restore`) and then build or run the solution (`dotnet build` / `dotnet test` / `dotnet run`) from the repository root.

