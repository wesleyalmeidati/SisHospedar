# SisHospedar

Sistema de gestão hoteleira desenvolvido em **ASP.NET Core MVC (.NET 8)** com **Entity Framework Core**, permitindo o controle de hóspedes, apartamentos, hospedagens, funcionários, produtos e consumo.

> Este projeto é a modernização do meu Trabalho de Conclusão de Curso, originalmente desenvolvido em **ASP.NET Web Forms (.NET Framework) com ADO.NET**. Reescrevi a aplicação usando o stack atual do ecossistema .NET, corrigindo problemas de segurança do projeto original e aplicando boas práticas de arquitetura.

## Funcionalidades

- Cadastro e gestão de **Hóspedes**, **Apartamentos**, **Funcionários** e **Produtos**
- Abertura de **Hospedagem** (associação hóspede + apartamento, com controle de disponibilidade)
- Lançamento de **Consumo** durante a estadia, com baixa automática de estoque
- **Finalização de hospedagem**, liberando o apartamento automaticamente
- Autenticação de funcionários com senha armazenada como **hash** (nunca em texto puro)
- Portal do hóspede: cadastro, login, criação de reserva online, acompanhamento da estadia e solicitação de encerramento pela própria conta

## Perfis de acesso

O sistema tem duas identidades de usuário independentes, cada uma com seu próprio login e sessão:

### Funcionário (equipe do hotel)

Acesso via `/Home/Login`. O cargo (`Cargo`) define o que cada funcionário enxerga no painel e quais ações pode executar:

| Cargo | Pode fazer |
|---|---|
| **Administrador** / **Gerente** | Acesso total: gerenciar funcionários, apartamentos, produtos, hóspedes, hospedagens, reservas e consumo |
| **Recepcionista** | Confirmar/cancelar reservas, abrir e finalizar hospedagens, lançar consumo |
| **Limpeza** / **Manutenção** | Consultar o status dos apartamentos |
| **Vallet** | Consultar hospedagens ativas |

### Hóspede (cliente)

Acesso via `/Cliente/Login` (conta separada da de funcionário). Permite ao hóspede:

- Criar uma reserva online (`Reserva/Create`), que fica **pendente** até a recepção confirmar (o que gera o check-in de fato)
- Acompanhar a estadia ativa e o consumo lançado em **Minha Conta**
- Solicitar prorrogação da data de saída
- Solicitar o encerramento da hospedagem, que a recepção então confirma

## Stack técnica

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core MVC (.NET 8) |
| Acesso a dados | Entity Framework Core (SQL Server) |
| Frontend | Razor Views + Bootstrap 5 |
| Autenticação | Cookie authentication nativa do ASP.NET Core (2 esquemas independentes) + `PasswordHasher` |

## O que mudou em relação à versão original (2013)

Este projeto começou como um sistema em **ASP.NET Web Forms + ADO.NET puro** (DAOs manuais com `SqlCommand`/`SqlDataReader`). Na modernização:

- **Segurança de credenciais**: a string de conexão com usuário e senha do banco, que estava fixa no código-fonte (`ConexaoBanco.cs`), foi movida para configuração externa (`appsettings.json` local, fora do controle de versão) — prática padrão de mercado.
- **Senhas de funcionário**: antes não havia hashing explícito documentado; agora toda senha é convertida em hash antes de ser salva, usando `PasswordHasher` do ASP.NET Core Identity.
- **Acesso a dados**: as classes DAO manuais (que já usavam corretamente queries parametrizadas, evitando SQL Injection) foram substituídas pelo Entity Framework Core, que gera essas queries de forma automática e segura, além de mapear relacionamentos entre entidades (ex: Hospedagem → Hóspede, Hospedagem → Apartamento).
- **Arquitetura**: a separação em camadas (Apresentação / Negócio / Acesso a Dados) do projeto original foi preservada conceitualmente, adaptada para o padrão MVC.

## Como rodar localmente

### Pré-requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server (local, Express ou LocalDB)

### Passos

1. Clone o repositório:
   ```bash
   git clone https://github.com/wesleysalesdev/SisHospedar.git
   cd SisHospedar
   ```

2. Copie o arquivo de configuração local (que não é versionado) e preencha com sua senha real:
   ```bash
   cp appsettings.Development.json.example appsettings.Development.json
   ```
   Edite `appsettings.Development.json` com os dados do seu SQL Server local.

3. Restaure os pacotes e crie o banco de dados (a migration inicial já está versionada no projeto, então só é preciso aplicá-la):
   ```bash
   dotnet restore
   dotnet tool install --global dotnet-ef   # se ainda não tiver
   dotnet ef database update
   ```

4. Rode a aplicação:
   ```bash
   dotnet run
   ```

5. Acesse `https://localhost:5001` (ou a porta indicada no terminal).

### Rodando os testes

```bash
dotnet test
```

Os testes (`SisHospedar.Tests/`) rodam contra um banco EF Core InMemory — não precisam do SQL Server nem do `appsettings.Development.json` configurado.

### Credenciais de teste

Na primeira execução contra um banco vazio, o `Program.cs` popula hóspedes, apartamentos, produtos e os funcionários de teste abaixo — todos com a senha **`Senha123`** (armazenada como hash):

| Login | Cargo |
|---|---|
| `admin` | Administrador |
| `gerente` | Gerente |
| `recepcao` | Recepcionista |
| `limpeza` | Limpeza |
| `vallet` | Vallet |

Para testar o portal do hóspede, crie uma conta nova em `/Cliente/Registrar` — os hóspedes seedados não têm login/senha (foram cadastrados como se fosse pela recepção, no balcão).

## Fluxo de uma estadia

```
Reserva (pendente)  →  Recepção confirma  →  Hospedagem (check-in)  →  Consumo(s)  →  Finalização (check-out)
     [Hóspede]              [Recepção]              [Recepção]           [Recepção]         [Recepção]
```

- O hóspede cria uma **Reserva** pelo site; ela fica `Pendente` até a recepção confirmar.
- Ao confirmar, a recepção realiza o check-in: cria a **Hospedagem** de verdade e o apartamento passa a `Ocupado`. Uma hospedagem também pode ser aberta diretamente no balcão, sem reserva prévia.
- Durante a estadia, a recepção lança **Consumo** (produtos consumidos), com baixa automática no estoque.
- Ao encerrar, a hospedagem é marcada como finalizada e o apartamento volta a `Disponível` automaticamente.

## Estrutura do projeto

```
SisHospedar/
├── Controllers/     # Lógica de cada entidade (Hospede, Apartamento, Hospedagem...)
├── Models/          # Entidades de domínio
├── Data/            # AppDbContext (Entity Framework Core)
├── Filters/         # ActionFilterAttributes de autorização (RequerFuncionario, RequerCargo)
├── Views/           # Telas Razor (CRUD por entidade)
├── wwwroot/         # Arquivos estáticos (CSS)
├── SisHospedar.Tests/  # Testes de integração (xUnit)
└── Program.cs       # Ponto de entrada e configuração da aplicação
```

## Próximos passos (roadmap)

- [x] Testes automatizados (xUnit) — autorização, baixa de estoque, transições de status de apartamento e confirmação de reserva
- [x] Autenticação por cookie nativa do ASP.NET Core (dois esquemas independentes, um por identidade) no lugar de sessão manual — ainda não é o Identity "completo" (sem `UserManager`/reset de senha/lockout); ver CLAUDE.md para o motivo da escolha
- [ ] Migração para ASP.NET Core Identity completo (`UserManager`/`SignInManager`, lockout, reset de senha) — avaliado e adiado por ser uma mudança de escala bem maior (dois `IdentityUser` independentes exigiriam múltiplos `DbContext`s ou truques de "shared entity types")
- [ ] Deploy em nuvem (Azure App Service)
- [ ] API REST para consumo externo

---

Desenvolvido por **Wesley Sales de Almeida** — [LinkedIn](https://linkedin.com/in/wesleysalesdev)
