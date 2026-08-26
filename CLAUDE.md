# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

SisHospedar is a hotel management system (ASP.NET Core MVC, .NET 8, EF Core, SQL Server, Razor + Bootstrap 5). It's a modernization of a .NET Framework/Web Forms/ADO.NET TCC project — the rewrite's explicit goals were removing the hardcoded connection string, hashing employee/guest passwords, and replacing manual ADO.NET DAOs with EF Core. Keep those goals in mind: don't reintroduce plaintext secrets/passwords or hand-rolled SQL.

## Commands

```bash
dotnet restore
dotnet build
dotnet run                              # serves at https://localhost:5001 (or port shown in terminal)

# EF Core migrations (from repo root)
dotnet tool install --global dotnet-ef  # one-time
dotnet ef migrations add <Name>
dotnet ef database update
```

There is no test project yet (see README roadmap — xUnit tests are planned but not present).

### Local setup

`appsettings.Development.json` is gitignored (contains the real DB password). Copy `appsettings.Development.json.example` to `appsettings.Development.json` and fill in a real SQL Server connection string before running. `Program.cs` throws on startup if `ConnectionStrings:DefaultConnection` is missing.

On first run against an empty database, `Program.cs` seeds sample Hóspedes, Apartamentos, Produtos, and Funcionários (all test employee accounts use password `Senha123`, hashed via `PasswordHasher`).

## Architecture

Standard ASP.NET Core MVC, no service/repository layer — controllers talk to `AppDbContext` (`Data/AppDbContext.cs`) directly via EF Core. One `DbContext`, no separate business-logic layer; validation and side effects (stock deduction, apartment status transitions) live inline in controller actions.

### Two separate user identities, both session-based (no ASP.NET Core Identity/cookie auth)

- **Funcionário (employee)** — `HomeController.Login` sets `Session["FuncionarioId"/"FuncionarioNome"/"FuncionarioCargo"]`. Employee-only controllers (e.g. `FuncionarioController`) check `Session.GetInt32("FuncionarioId")` at the top of each action (see `VerificarPermissao()` pattern) and redirect to `Home/Login` if absent. Role (`CargoFuncionario`) gates finer-grained actions — e.g. only `Administrador`/`Gerente` can manage staff.
- **Hóspede (guest/customer)** — `ClienteController.Login`/`Registrar` sets `Session["HospedeId"/"HospedeNome"]`, independent of the employee session. Guest-facing self-service actions (`MinhaConta`, `Prorrogar`, `SolicitarEncerramento`, `Reserva/Create`) check this session key and redirect to `Cliente/Login` if absent.

There's no shared `[Authorize]`/middleware-based auth — each controller repeats its own session check inline. When adding a new controller/action, follow the existing per-controller session-check pattern for whichever identity it belongs to, matching the sibling controllers rather than inventing a new mechanism.

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
