# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

SisHospedar is a hotel management system (ASP.NET Core MVC, .NET 8, EF Core, SQL Server, Razor + Bootstrap 5). It's a modernization of a .NET Framework/Web Forms/ADO.NET TCC project — the rewrite's explicit goals were removing the hardcoded connection string, hashing employee/guest passwords, and replacing manual ADO.NET DAOs with EF Core. Keep those goals in mind: don't reintroduce plaintext secrets/passwords or hand-rolled SQL.

## Commands

```bash
dotnet restore
dotnet build                            # builds the whole solution (SisHospedar.sln): web app + tests
dotnet run                              # serves at https://localhost:5001 (or port shown in terminal)
dotnet test                             # runs SisHospedar.Tests (no SQL Server needed, see below)

# EF Core migrations (from repo root)
dotnet tool install --global dotnet-ef  # one-time
dotnet ef migrations add <Name>
dotnet ef database update
```

### Tests (`SisHospedar.Tests/`)

xUnit integration tests using `WebApplicationFactory<Program>` — boots the real app in-memory and swaps the SQL Server `AppDbContext` registration for **in-memory SQLite** (`CustomWebApplicationFactory`), so `dotnet test` needs no real database. SQLite (not EF Core's "InMemory" provider) is required because `ConsumoController.Create`'s atomic stock decrement uses `ExecuteUpdateAsync`, which the InMemory provider cannot translate — SQLite is a real relational engine and supports it. `Program.cs`'s own seed data runs against that database on host startup, so tests reuse the known seeded logins (`admin`/`gerente`/`recepcao`/etc., password `Senha123`) instead of re-seeding; tests needing specific scenario data (a room in a given `StatusApartamento`, a `Consumo` against a controlled stock level, etc.) insert it directly via `CustomWebApplicationFactory.UsandoContextoAsync`.

`SisHospedar.Tests` lives as a subfolder of the main project's directory (not a sibling) — `Microsoft.NET.Sdk.Web`'s default `**/*.cs` glob would otherwise try to compile it into the web project too, so `SisHospedar.csproj` explicitly excludes it (`<Compile Remove="SisHospedar.Tests/**/*.cs" />`). `Program.cs` ends with `public partial class Program { }` so the test project can reference it — top-level-statement `Program` is `internal` by default, and that's a hard C# accessibility-domain rule that plain `InternalsVisibleTo` does not work around for generic base classes like `WebApplicationFactory<Program>`.

Coverage: `AutorizacaoFuncionarioTests` (anonymous access redirects, wrong `Cargo` gets 403, guest vs. employee identity separation) and business-logic tests — `ConsumoTests` (stock deduction, insufficient-stock rejection, rejecting consumption on a finalized stay), `HospedagemStatusTests` (check-in occupies the room, check-out frees it), `ReservaConfirmarTests` (confirming a booking creates the `Hospedagem` and occupies the room, or fails gracefully if the room is no longer free). Shared HTTP/login/antiforgery helpers live in `TestesIntegracaoBase`. Writing `HospedagemStatusTests.FinalizarConfirmado_LiberaApartamento` caught a real, pre-existing bug: `Views/Hospedagem/Finalizar.cshtml`'s form posted a `HospedagemId` field while the action expected a route parameter named `id` (no `asp-route-id`), so the "Confirmar Finalização" button always 404'd in the real app — fixed by adding `asp-route-id="@Model.HospedagemId"` to the form.

### CI (`.github/workflows/ci.yml`)

Runs on every push/PR to `main`: restore, build (Release), `dotnet test`, then `dotnet ef migrations has-pending-model-changes` — the last step is what would have caught the `InitialCreate`-vs-model drift found earlier in this project (missing `Hospede.Login`/`Reservas`/etc.) before it ever reached a clone. None of this needs a real SQL Server: the workflow sets `ConnectionStrings__DefaultConnection` to a dummy value purely to satisfy `Program.cs`'s startup guard clause — tests run against in-memory SQLite, and the pending-migrations check only compares the in-code model against the last migration's snapshot, no live connection required.

### Local setup

`appsettings.Development.json` is gitignored (contains the real DB password). Copy `appsettings.Development.json.example` to `appsettings.Development.json` and fill in a real SQL Server connection string before running. `Program.cs` throws on startup if `ConnectionStrings:DefaultConnection` is missing.

On first run against an empty database, `Program.cs` seeds sample Hóspedes, Apartamentos, Produtos, and Funcionários (all test employee accounts use password `Senha123`, hashed via `PasswordHasher`).

## Architecture

Standard ASP.NET Core MVC, one `DbContext` (`Data/AppDbContext.cs`), no repository layer. Read-only queries (`Index`/`Details` listings, `SelectList` sources) stay inline in controllers via `_context` directly — no repository wrapping needed for those. Non-trivial business logic with side effects (stock deduction, `Apartamento.Status` transitions, reservation confirmation) instead lives in `Services/`: `IHospedagemService` (check-in/check-out), `IConsumoService` (consumption + atomic stock decrement), `IReservaService` (guest booking creation + reception confirm/cancel). Each service method returns `(bool Sucesso, string? Erro)` (or a small enum/nullable-string variant where the caller needs different branching, like `NotFound()` vs. a `ModelState` error) — controllers translate that into `ModelState.AddModelError`/`TempData`/redirects, they don't re-derive the business rule. When adding new business logic with side effects, add it to the relevant service (or a new one) rather than back into the controller — that's what made this logic unit-testable via `SisHospedar.Tests` without going through the full HTTP pipeline for every case, and it's also what keeps the 22 existing tests as a regression net: they were written before the service extraction and still pass unchanged against it.

### Two separate user identities, both session-based (no ASP.NET Core Identity/cookie auth)

- **Funcionário (employee)** — `HomeController.Login` sets `Session["FuncionarioId"/"FuncionarioNome"/"FuncionarioCargo"]`. Employee-only controllers/actions are gated by two `ActionFilterAttribute`s in `Filters/`: `[RequerFuncionario]` (just requires an active employee session; redirects to `Home/Login` if absent) and `[RequerCargo(params CargoFuncionario[])]` (also requires the session's `Cargo` to be one of those listed; otherwise 403 — used on `FuncionarioController` for `Administrador`/`Gerente`-only staff management). Apply the attribute at the class level when every action needs it (see `HospedagemController`, `ApartamentoController`, etc.); apply it per-action when a controller mixes identities, like `ReservaController` (guest `Create` vs. employee `Index`/`Confirmar`/`Cancelar`).
- **Hóspede (guest/customer)** — `ClienteController.Login`/`Registrar` sets `Session["HospedeId"/"HospedeNome"]`, independent of the employee session. Guest-facing self-service actions (`MinhaConta`, `Prorrogar`, `SolicitarEncerramento`, `Reserva/Create`) check this session key inline and redirect to `Cliente/Login` if absent — this side hasn't been moved to a filter yet.

When adding a new employee-only controller/action, use `[RequerFuncionario]`/`[RequerCargo]` rather than reintroducing an inline session check — the whole point of the filters is that forgetting to protect a new action is no longer possible by omission the way a copy-pasted check could be forgotten (this happened for real: 5 controllers once shipped with zero session checks before the filters existed).

### Domain flow: Reserva → Hospedagem → Consumo

- `Reserva` = a guest's online booking request (`StatusReserva`: Pendente/Confirmada/Cancelada). Created by the guest via `ReservaController.Create` (guest-side).
- Reception confirms via `ReservaController.Confirmar` (employee-side), which is the check-in point: it creates the actual `Hospedagem` record and flips the `Apartamento.Status` to `Ocupado`. `Hospedagem` can also be created directly by reception without a prior `Reserva` (`HospedagemController.Create`).
- `Consumo` (product consumption during a stay) is added via `ConsumoController.Create`, which decrements `Produto.Estoque` and is tied to a `Hospedagem`.
- Ending a stay is `HospedagemController.Finalizar`/`FinalizarConfirmado` (employee side — sets `Finalizada = true`, `DataSaida`, and frees the apartment back to `Disponivel`) or the guest can flag `SolicitouEncerramento = true` via `ClienteController.SolicitarEncerramento`, which reception then acts on.
- `Apartamento.Status` (`Disponivel`/`Ocupado`/`Manutencao`) is the single source of truth for booking eligibility — every action that creates/confirms/ends a stay must keep it in sync; there's no separate availability-calculation logic.

### EF Core model notes (`Data/AppDbContext.cs`)

- Unique indexes: `Hospede.Cpf`, `Hospede.Login`, `Funcionario.Login`, `Apartamento.Numero`.
- FKs to `Hospede`/`Apartamento` from `Reserva`/`Hospedagem` use `DeleteBehavior.Restrict` (no cascade); `Consumo → Hospedagem` cascades, `Consumo → Produto` restricts.
- Money fields (`Apartamento.ValorDiaria`, `Produto.Preco`, `Consumo.ValorUnitario`) are `decimal(10,2)`.
- `Hospedagem.TotalConsumo` and `Consumo.ValorTotal` are computed `[NotMapped]` properties, not persisted columns.

### Passwords

Both `Funcionario.SenhaHash` and `Hospede.SenhaHash` are hashed with `Microsoft.AspNetCore.Identity.PasswordHasher<T>` (instantiated per-controller, not DI-registered). Plaintext password only ever appears as a transient action-method parameter (`senha`), never bound onto the model (`ModelState.Remove(nameof(X.SenhaHash))` before validation) and never persisted or logged.
