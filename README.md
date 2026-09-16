# SisHospedar

> **Demonstração online desativada em 16/09/2026.** O projeto esteve no ar em Azure Container Apps + Azure SQL de 07/09 a 16/09/2026; a infraestrutura foi desligada por decisão de custo após a conclusão do TCC — os números e o raciocínio estão em [Desativação da infraestrutura](#desativação-da-infraestrutura). O sistema roda localmente em poucos minutos: ver [Como rodar localmente](#como-rodar-localmente).

Sistema de gestão hoteleira desenvolvido em **ASP.NET Core MVC (.NET 10)** com **Entity Framework Core**, permitindo o controle de hóspedes, apartamentos, reservas, hospedagens, funcionários, produtos e consumo.

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
| Backend | ASP.NET Core MVC (.NET 10) |
| Acesso a dados | Entity Framework Core (SQL Server) |
| Frontend | Razor Views + Bootstrap 5, com design system próprio (`wwwroot/css/site.css`) |
| Autenticação | Cookie authentication nativa do ASP.NET Core (2 esquemas independentes) + ASP.NET Core Identity (`UserManager`/`SignInManager`, hash, lockout, reset de senha) |
| Deploy | Docker (`Dockerfile` multi-stage) → Docker Hub → Azure Container Apps, banco em Azure SQL Database *(desativado em 16/09/2026)* |

## O que mudou em relação à versão original (2013)

Este projeto começou como um sistema em **ASP.NET Web Forms + ADO.NET puro** (DAOs manuais com `SqlCommand`/`SqlDataReader`). Na modernização:

- **Segurança de credenciais**: a string de conexão com usuário e senha do banco, que estava fixa no código-fonte (`ConexaoBanco.cs`), foi movida para configuração externa (`appsettings.json` local, fora do controle de versão) — prática padrão de mercado.
- **Senhas de funcionário**: antes não havia hashing explícito documentado; agora toda senha é convertida em hash antes de ser salva, usando `PasswordHasher` do ASP.NET Core Identity.
- **Acesso a dados**: as classes DAO manuais (que já usavam corretamente queries parametrizadas, evitando SQL Injection) foram substituídas pelo Entity Framework Core, que gera essas queries de forma automática e segura, além de mapear relacionamentos entre entidades (ex: Hospedagem → Hóspede, Hospedagem → Apartamento).
- **Arquitetura**: a separação em camadas (Apresentação / Negócio / Acesso a Dados) do projeto original foi preservada conceitualmente, adaptada para o padrão MVC.

## Como rodar localmente

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
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

Na primeira execução contra um banco vazio, o `Program.cs` popula hóspedes, apartamentos, produtos e um funcionário para cada cargo — senha sempre armazenada como hash, login no padrão `primeiro.sobrenome` (o mesmo que o sistema gera sozinho ao cadastrar um funcionário novo).

Para conhecer o sistema, use a conta de **Gerente**, que tem acesso total:

| Login | Senha | Cargo |
|---|---|---|
| `luan.santos` | `Senha123` | Gerente |

As outras contas do seed existem para exercitar as telas específicas de cada cargo em ambiente local; os logins saem dos nomes listados em `Program.cs`, seguindo o mesmo padrão. A conta de Administrador teve a senha trocada no ambiente publicado e não usa mais a senha do seed.

Para testar o portal do hóspede, crie uma conta nova em `/Cliente/Registrar` — os hóspedes seedados não têm login/senha (foram cadastrados como se fosse pela recepção, no balcão).

## Deploy

> A infraestrutura descrita nesta seção **foi desativada em 16/09/2026** (ver [Desativação da infraestrutura](#desativação-da-infraestrutura)). O que está documentado aqui continua válido como registro do que foi construído e como reconstruir — a imagem no Docker Hub e as migrations no repositório são suficientes para subir tudo de novo.

A aplicação rodava em produção como **container** — mesma imagem Docker que builda localmente, sem código específico de nuvem:

```
Dockerfile (multi-stage: build com o SDK, imagem final só com o runtime)
   → docker build / docker push → Docker Hub (wesleysales/sishospedar)
   → Azure Container Apps (env-sishospedar, plano de consumo)
   → Azure SQL Database (sql-sishospedar-wsales-br, tier Basic)
```

Motivo de Container Apps em vez de App Service: a assinatura usada não tinha cota liberada pro tier gratuito (F1) do App Service em nenhuma das ~9 regiões testadas — comum em assinaturas novas. Container Apps usa outro pool de cota (consumo serverless, não VM reservada) e não esbarrou nessa restrição.

### Redeploy — automático via CI/CD

> O job `deploy` foi **removido** do `.github/workflows/ci.yml` em 16/09/2026, junto com a desativação dos recursos no Azure — sem o Container App do outro lado, ele falharia em todo push. O CI segue rodando build, testes e verificação de *drift* de migration. O parágrafo abaixo descreve como o deploy funcionava.

Todo push em `main` que passava no `build-and-test` disparava o job `deploy` (`.github/workflows/ci.yml`): buildava a imagem, tagueava com o SHA curto do commit (nunca `:latest` sozinho — ver a nota abaixo), dava push no Docker Hub, autenticava no Azure via um Service Principal com Contributor restrito ao resource group `rg-sishospedar` (secret `AZURE_CREDENTIALS`) e rodava `az containerapp update --image wesleysales/sishospedar:<sha>`. Não era preciso rodar nada manualmente — só dar merge/push em `main`.

**Por que tag com SHA em vez de `:latest`**: o Container Apps só cria uma revisão nova quando a *string* de imagem muda. Com `:latest` fixo, um `update` pode "ter sucesso" sem detectar que o conteúdo da imagem mudou, e o site continua servindo a revisão antiga (isso aconteceu de verdade — foi assim que o bug do símbolo "¤" ficou no ar mesmo depois do primeiro redeploy). Tagueando com o SHA do commit, toda imagem tem uma string diferente, então toda atualização vira revisão nova de forma garantida.

Se precisar redeployar manualmente (fora do CI):

```bash
docker build -t wesleysales/sishospedar:manual-$(date +%s) .
docker push wesleysales/sishospedar:manual-$(date +%s)
az containerapp update --name sishospedar --resource-group rg-sishospedar --image wesleysales/sishospedar:manual-$(date +%s)
```

Migrations **não** são aplicadas automaticamente pelo CI (de propósito — nunca rodar algo potencialmente destrutivo sem supervisão direta). Se uma migration nova foi criada, aplique contra o banco de produção manualmente antes do próximo deploy (substitua a senha pela guardada com você, nunca commitada):

```bash
ConnectionStrings__DefaultConnection="Server=tcp:sql-sishospedar-wsales-br.database.windows.net,1433;Database=SisHospedar;User ID=sqladmin;Password=<senha>;Encrypt=True;TrustServerCertificate=False;" \
dotnet ef database update
```

(Precisa liberar seu IP no firewall do SQL Server antes: `az sql server firewall-rule create --resource-group rg-sishospedar --server sql-sishospedar-wsales-br --name AllowMyIP --start-ip-address <seu-ip> --end-ip-address <seu-ip>` — remova a regra depois com `az sql server firewall-rule delete`.)

### Desativação da infraestrutura

O resource group `rg-sishospedar` foi deletado em **16/09/2026**, encerrando a hospedagem. A decisão saiu de medição, não de estimativa — e o que ela revelou vale mais do que a economia.

A premissa inicial era que o Container Apps caberia no tier gratuito (este README chegou a afirmar isso). A fatura real, consultada via Cost Management API com granularidade diária e quebra por serviço, mostrou outra coisa:

| Data | Container Apps | SQL Basic |
|---|---|---|
| 07/09 (parcial) | R$ 0,00 | R$ 0,34 |
| 08–10/09 | **R$ 0,00** | R$ 1,01/dia |
| 11/09 | R$ 0,34 | R$ 1,01 |
| 12–15/09 | **R$ 2,01/dia** | R$ 1,01/dia |

O Container Apps custou exatamente zero por quatro dias e então ligou. A causa é aritmética: a cota gratuita é de **180.000 vCPU-segundos e 360.000 GiB-segundos por mês**, e `minReplicas: 1` com 0,5 vCPU consome 43.200 vCPU-s/dia — a cota dura **4,2 dias**. Os recursos nasceram em 07/09 às 16h; 07/09 + 4,2 dias cai em 11/09, exatamente onde o custo subiu. A cota reseta todo mês, então o padrão se repetiria: ~4 dias grátis e ~26 dias pagos.

Custo em regime: **~R$ 90/mês** — R$ 30 de SQL Basic e R$ 60 de container ocioso, para um site de demonstração praticamente sem tráfego. Dois terços da conta eram a aplicação esperando visita.

Duas conferências independentes validaram os números: o SQL Basic tem preço de tabela de US$ 4,90/mês (≈ R$ 30 ✓), e a taxa *idle* do Container Apps aplicada a 43.200 vCPU-s + 86.400 GiB-s/dia dá ≈ US$ 0,39/dia (≈ R$ 2,10 ✓).

**O que foi descartado por não compensar.** Migrar o SQL para *serverless com auto-pause* parecia a saída óbvia — e o código até colabora, porque `FlushVisitasService.DescarregarAsync` retorna antes de abrir conexão quando não há visitas, e `/health` deliberadamente não consulta o banco. Mas o auto-pause tem atraso mínimo de 60 minutos e o serverless cobra por vCore-hora ativa: uma única visita no dia acorda o banco por uma hora inteira, e ~19 horas ativas no mês já empatam com o Basic. Sairia mais caro, com *cold start* de banco de brinde.

**O que sobrevive à desativação.** Nada insubstituível estava só na nuvem:

| Artefato | Onde vive |
|---|---|
| Código | este repositório |
| Schema do banco | as 9 migrations em `Migrations/` |
| Dados de exemplo | o seed no `Program.cs` |
| Imagem executável | Docker Hub, `wesleysales/sishospedar` |

O banco de produção continha apenas seed, contadores de audiência e hóspedes fictícios de teste — por isso não foi feito export `.bacpac`, o que também evitou criar uma Storage Account só para guardá-lo.

**Como trazer de volta**, se algum dia for necessário: recriar o resource group, o Azure SQL e o Container App apontando para a imagem no Docker Hub, rodar `dotnet ef database update` contra o banco novo e restaurar o job `deploy` no CI a partir do histórico do Git. Leva algo em torno de 20–30 minutos. Para rodar a aplicação e vê-la funcionando, porém, não é preciso nada disso — o [passo a passo local](#como-rodar-localmente) resolve em poucos minutos e não depende de nuvem nenhuma.

## E-mail

A tela de "esqueci minha senha" (`/Conta/EsqueciSenha`) pede o **e-mail** cadastrado, não o login — mais fácil de lembrar, já que o login de funcionário é gerado automaticamente pelo sistema. `Hospede.Email`/`Funcionario.Email` são únicos por tabela (índice filtrado, permite qualquer número de contas sem e-mail), então a busca nunca é ambígua. O link sai por e-mail de verdade, via `IEmailSender`/`SmtpEmailSender` (`Services/`) usando SMTP do Gmail com uma [senha de app](https://myaccount.google.com/apppasswords) — não a senha normal da conta (exige verificação em duas etapas ativa). Configuração (`Email:Remetente`/`Email:SenhaApp`, mais `Email:SmtpHost`/`Email:SmtpPort` opcionais) nunca fica no código: local vem de `appsettings.Development.json` (gitignored, copie de `appsettings.Development.json.example`); em produção, vem de variáveis de ambiente no Container App, com a senha guardada como *secret* do Container Apps (mesmo padrão da connection string do SQL):

```bash
az containerapp secret set --name sishospedar --resource-group rg-sishospedar --secrets email-senha-app="<senha-de-app>"
az containerapp update --name sishospedar --resource-group rg-sishospedar --set-env-vars \
  "Email__Remetente=<seu-email>@gmail.com" \
  "Email__SenhaApp=secretref:email-senha-app"
```

Só funciona para contas que têm e-mail cadastrado — `Hospede.Email` (preenchido no autocadastro em `/Cliente/Registrar`) ou `Funcionario.Email` (opcional, preenchido em `/Funcionario/Create`). Contas sem e-mail (a maioria das seedadas por `Program.cs`, que existem só para teste) precisam de um Administrador/Gerente pra redefinir a senha manualmente — não há outro caminho de autoatendimento. Nos testes de integração (`SisHospedar.Tests`), `IEmailSender` é substituído por uma `FakeEmailSender` que só captura o e-mail em memória, sem enviar nada de verdade (ver `CustomWebApplicationFactory`/`IdentityTests`).

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
├── Dockerfile           # Build multi-stage usado no deploy (Container Apps)
└── Program.cs           # Ponto de entrada, DI, autenticação e seed de dados
```

## Próximos passos (roadmap)

- [x] Testes automatizados (xUnit) — autorização, baixa de estoque, transições de status de apartamento, confirmação de reserva, bloqueio de conta, redefinição de senha e validação do autocadastro (28 testes ao todo)
- [x] Autenticação por cookie nativa do ASP.NET Core (dois esquemas independentes, um por identidade) no lugar de sessão manual
- [x] Login único com detecção automática de identidade (funcionário x hóspede) e geração automática de login de funcionário
- [x] Prevenção de reserva duplicada (conflito de datas) e status `Reservado` no apartamento
- [x] Redesign completo da interface (design system próprio, fora do Bootstrap padrão)
- [x] Migração para ASP.NET Core Identity completo (`UserManager`/`SignInManager`, lockout, reset de senha) — via uma store customizada (`EntityUserStore`, ver `CLAUDE.md`) em vez de herdar `IdentityUser<T>`, evitando a duplicação de tabelas de apoio que o caminho "óbvio" exigiria para duas identidades independentes
- [x] Deploy em nuvem — Docker + Docker Hub + Azure Container Apps + Azure SQL Database (ver seção [Deploy](#deploy)); infraestrutura desativada em 16/09/2026 por decisão de custo, ver [Desativação da infraestrutura](#desativação-da-infraestrutura)
- [x] E-mail de verdade para o link de redefinição de senha — `IEmailSender`/`SmtpEmailSender` via Gmail (ver seção [E-mail](#e-mail))
- [x] CI/CD automatizado — build + testes + push + `containerapp update` a cada push em `main` (ver `.github/workflows/ci.yml`), tagueando a imagem com o SHA do commit em vez de `:latest`
- [x] Rate limiting por IP em login, registro e "esqueci minha senha" (`Microsoft.AspNetCore.RateLimiting`, nativo do ASP.NET Core, sem custo) — complementa o lockout por conta do Identity, que trava uma conta específica mas não impede alguém de varrer várias contas diferentes a partir do mesmo IP
- [x] Validação real de CPF (dígito verificador via módulo 11, não só formato) e campos obrigatórios no autocadastro do hóspede
- [x] Atualização de .NET 8 para **.NET 10** (LTS) — target framework, EF Core 10, imagens base do Docker e CI. Duas coisas apareceram no caminho: o pacote `Microsoft.AspNetCore.Identity` 2.2.0 era um resquício da era ASP.NET Core 2.2 e foi removido (o Identity já vem no shared framework de um projeto `Sdk.Web`), e a troca de provider nos testes precisou de um ajuste — ver a nota sobre `IDbContextOptionsConfiguration` no `CLAUDE.md`. Nenhuma migration nova foi necessária: o snapshot gerado pelo EF Core 8 continua consistente sob o EF Core 10, então o banco de produção não mudou.

### Escopo final

Este projeto está considerado completo para o que se propôs: modernizar o TCC de 2013, corrigindo os problemas de segurança da versão original (senha em texto puro, string de conexão fixa no código, SQL manual) e reconstruindo com arquitetura atual — EF Core, ASP.NET Core Identity de verdade, testes automatizados, deploy em nuvem com CI/CD. Dois itens ficaram de fora por decisão, não por falta de tempo:

- **Pagamento**: não vai ser implementado. É um projeto de aprendizado, sem intenção de comercialização — integrar um gateway de verdade (Stripe/Mercado Pago/PagSeguro) traria responsabilidades (dados de cartão, estornos, compliance) desproporcionais ao que o projeto se propõe a demonstrar, sem agregar aprendizado que o resto do stack já não cubra.
- **API REST**: fica como ideia em aberto, não como próximo passo planejado — o sistema já atende suas próprias necessidades via MVC; uma API só faria sentido diante de um consumidor externo de verdade (app mobile, integração de terceiros), que não existe hoje.

---

Desenvolvido por **Wesley Sales de Almeida** — [LinkedIn](https://linkedin.com/in/wesleysalesdev)
