# SisHospedar

Sistema de gestão hoteleira desenvolvido em **ASP.NET Core MVC (.NET 8)** com **Entity Framework Core**, permitindo o controle de hóspedes, apartamentos, reservas, hospedagens, funcionários, produtos e consumo.

> Este projeto é a modernização do meu Trabalho de Conclusão de Curso, originalmente desenvolvido em **ASP.NET Web Forms (.NET Framework) com ADO.NET**. Reescrevi a aplicação usando o stack atual do ecossistema .NET, corrigindo problemas de segurança do projeto original e aplicando boas práticas de arquitetura.

## Funcionalidades

- Cadastro e gestão de **Hóspedes**, **Apartamentos**, **Funcionários** e **Produtos**
- **Reserva online** pelo hóspede, com bloqueio automático de conflito de datas (não deixa duas pessoas pedirem o mesmo apartamento no mesmo período)
- Apartamento com status **Disponível → Reservado → Ocupado**, refletindo o ciclo reserva → check-in → check-out sem cálculo de disponibilidade separado
- Painel da recepção avisa quando há **hóspede chegando no dia** (comparando com a data do servidor), com confirmação de check-in em um clique
- Lançamento de **Consumo** durante a estadia, com baixa automática de estoque
- **Finalização de hospedagem** (checkout), conferindo os itens consumidos e liberando o apartamento automaticamente
- **Login único**: a mesma tela identifica sozinha se as credenciais são de um funcionário ou de um hóspede e encaminha para a área certa — sem precisar escolher "sou funcionário" ou "sou hóspede" antes de entrar
- Login de funcionário é **gerado automaticamente** a partir do nome (padrão `primeiro.sobrenome`, com resolução de colisão); só o hóspede escolhe o próprio login, no autocadastro
- Autenticação com senha em **hash** (nunca em texto puro), bloqueio de conta após tentativas inválidas seguidas e **redefinição de senha** — tudo via `UserManager`/`SignInManager` do ASP.NET Core Identity de verdade
- Portal do hóspede: cadastro, login, criação de reserva, acompanhamento da estadia/consumo e solicitação de encerramento pela própria conta
- Interface própria (não é o Bootstrap padrão): design minimalista de inspiração editorial, com fotos reais compatíveis com cada tipo de acomodação e vídeo de fundo na home

## Perfis de acesso

O sistema tem duas identidades de usuário independentes (cookies e tabelas separadas), mas **uma única tela de login** (`/Home/Login` e `/Cliente/Login` levam à mesma página) — o sistema tenta as credenciais como funcionário e depois como hóspede, e redireciona para o painel ou para "Minha Conta" de acordo com qual bateu.

### Funcionário (equipe do hotel)

O cargo (`Cargo`) define o que cada funcionário enxerga no painel e quais ações pode executar:

| Cargo | Pode fazer |
|---|---|
| **Administrador** / **Gerente** | Acesso total: gerenciar funcionários, apartamentos, produtos, hóspedes, hospedagens, reservas e consumo |
| **Recepcionista** | Ver chegadas do dia, confirmar/cancelar reservas, abrir e finalizar hospedagens, lançar consumo |
| **Limpeza** / **Manutenção** | Consultar o status dos apartamentos |
| **Vallet** | Consultar hospedagens ativas |

### Hóspede (cliente)

Permite ao hóspede:

- Criar uma reserva online (`Reserva/Create`), que fica **pendente** até a recepção confirmar (o que gera o check-in de fato)
- Acompanhar a estadia ativa e o consumo lançado em **Minha Conta**
- Solicitar prorrogação da data de saída
- Solicitar o encerramento da hospedagem, que a recepção então confirma

## Stack técnica

| Camada | Tecnologia |
|---|---|
| Backend | ASP.NET Core MVC (.NET 8) |
| Acesso a dados | Entity Framework Core (SQL Server) |
| Frontend | Razor Views + Bootstrap 5, com design system próprio (`wwwroot/css/site.css`) |
| Autenticação | Cookie authentication nativa do ASP.NET Core (2 esquemas independentes) + ASP.NET Core Identity (`UserManager`/`SignInManager`, hash, lockout, reset de senha) |

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

5. Acesse `http://localhost:5000` (ou a porta indicada no terminal).

### Rodando os testes

```bash
dotnet test
```

Os testes (`SisHospedar.Tests/`) sobem a aplicação real em memória e trocam o SQL Server por **SQLite em memória** — um banco relacional de verdade (não o provider "InMemory" do EF Core, que não sabe traduzir a baixa atômica de estoque usada em `ConsumoController`). Não precisam do SQL Server nem do `appsettings.Development.json` configurado.

### Credenciais de teste

Na primeira execução contra um banco vazio, o `Program.cs` popula hóspedes, apartamentos, produtos e os funcionários de teste abaixo — todos com a senha **`Senha123`** (armazenada como hash). O login de cada um segue o padrão `primeiro.sobrenome`, o mesmo gerado automaticamente ao cadastrar um funcionário novo pelo sistema:

| Login | Cargo |
|---|---|
| `wesley.sales` | Administrador |
| `luan.santos` | Gerente |
| `matias.sampaio` | Recepcionista |
| `camila.rocha` | Limpeza |
| `diego.fernandes` | Vallet |

Para testar o portal do hóspede, crie uma conta nova em `/Cliente/Registrar` — os hóspedes seedados não têm login/senha (foram cadastrados como se fosse pela recepção, no balcão).

## Fluxo de uma estadia

```
Reserva (pendente)  →  Recepção confirma  →  Hospedagem (check-in)  →  Consumo(s)  →  Finalização (check-out)
     [Hóspede]              [Recepção]              [Recepção]           [Recepção]         [Recepção]

Apartamento:  Disponível  →  Reservado  →  Ocupado  →  Disponível
```

- O hóspede cria uma **Reserva** pelo site; ela fica `Pendente` até a recepção confirmar, e o apartamento já muda para `Reservado` — sai das listas de "disponível para reservar/check-in", evitando que outro hóspede peça o mesmo período. Um pedido com data conflitante é rejeitado antes mesmo de chegar nessa etapa.
- No dia da chegada, o painel da recepção mostra um aviso com o nome do hóspede e o apartamento, comparando a data de entrada da reserva com a data do próprio servidor.
- Ao confirmar, a recepção realiza o check-in: cria a **Hospedagem** de verdade e o apartamento passa a `Ocupado`. Uma hospedagem também pode ser aberta diretamente no balcão, sem reserva prévia (nesse caso o apartamento precisa estar `Disponível`).
- Durante a estadia, a recepção lança **Consumo** (produtos consumidos), com baixa automática no estoque.
- Ao encerrar, a recepção confere os itens consumidos, a hospedagem é marcada como finalizada e o apartamento volta a `Disponível` automaticamente.

## Estrutura do projeto

```
SisHospedar/
├── Controllers/         # Lógica de cada entidade (Hospede, Apartamento, Hospedagem...)
├── Models/              # Entidades de domínio
├── Data/                # AppDbContext (Entity Framework Core)
├── Services/            # Regras de negócio com efeito colateral (check-in/out, consumo,
│                         #   reserva, login unificado) — controllers só cuidam de ModelState/View
├── Authentication/      # Nomes dos esquemas de cookie (Funcionario / Hospede)
├── Filters/             # ActionFilterAttributes de autorização (RequerFuncionario, RequerCargo, RequerHospede)
├── Views/               # Telas Razor (CRUD por entidade + páginas públicas)
├── wwwroot/             # Arquivos estáticos (CSS, vídeo do hero — ver wwwroot/videos/README.md)
├── Migrations/          # Migrations do Entity Framework Core
├── SisHospedar.Tests/   # Testes de integração (xUnit + SQLite em memória)
└── Program.cs           # Ponto de entrada, DI, autenticação e seed de dados
```

## Próximos passos (roadmap)

- [x] Testes automatizados (xUnit) — autorização, baixa de estoque, transições de status de apartamento, confirmação de reserva, bloqueio de conta e redefinição de senha
- [x] Autenticação por cookie nativa do ASP.NET Core (dois esquemas independentes, um por identidade) no lugar de sessão manual
- [x] Login único com detecção automática de identidade (funcionário x hóspede) e geração automática de login de funcionário
- [x] Prevenção de reserva duplicada (conflito de datas) e status `Reservado` no apartamento
- [x] Redesign completo da interface (design system próprio, fora do Bootstrap padrão)
- [x] Migração para ASP.NET Core Identity completo (`UserManager`/`SignInManager`, lockout, reset de senha) — via uma store customizada (`EntityUserStore`, ver `CLAUDE.md`) em vez de herdar `IdentityUser<T>`, evitando a duplicação de tabelas de apoio que o caminho "óbvio" exigiria para duas identidades independentes; redefinição de senha em modo demonstração (mostra o link na tela em vez de enviar e-mail, já que o projeto não tem provedor de e-mail configurado)
- [ ] E-mail de verdade para o link de redefinição de senha (trocar o modo demonstração por um `IEmailSender`)
- [ ] Deploy em nuvem (Azure App Service)
- [ ] API REST para consumo externo
- [ ] Pagamento — via integração com um gateway de mercado (Stripe/Mercado Pago/PagSeguro), não implementação própria; deixado como última etapa de propósito, para não acoplar o modelo de domínio a isso antes de ele estar maduro

---

Desenvolvido por **Wesley Sales de Almeida** — [LinkedIn](https://linkedin.com/in/wesleysalesdev)
