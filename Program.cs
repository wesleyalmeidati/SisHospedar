using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Authentication;
using SisHospedar.Data;
using SisHospedar.Models;
using SisHospedar.Services;

// PT: Sem isso, ValorDiaria.ToString("C")/Preco.ToString("C") (usado em ~12 lugares
//     nas Views) formata usando a cultura do SO em vez de pt-BR — no Windows local
//     normalmente já sai "R$" porque o SO está configurado em pt-BR, mas no container
//     Linux de produção (sem locale pt-BR configurada) sai "¤" (símbolo genérico de
//     moeda do .NET quando não sabe qual usar). Setar aqui, uma vez, corrige "C" em
//     toda a aplicação sem precisar passar a cultura em cada ToString("C") individual.
// EN: Without this, ValorDiaria.ToString("C")/Preco.ToString("C") (used in ~12 places
//     across the Views) formats using the OS's culture instead of pt-BR — on local
//     Windows it usually already shows "R$" because the OS is set to pt-BR, but on
//     the production Linux container (no pt-BR locale configured) it shows "¤" (.NET's
//     generic currency placeholder when it doesn't know which one to use). Setting
//     this once here fixes "C" formatting across the whole app without having to pass
//     the culture to every individual ToString("C") call.
var culturaPadrao = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culturaPadrao;
CultureInfo.DefaultThreadCurrentUICulture = culturaPadrao;

var builder = WebApplication.CreateBuilder(args);

// PT: A string de conexão vem do appsettings.json (ambiente local) ou de
//     variável de ambiente / User Secrets — NUNCA fica escrita direto no código,
//     diferente da versão original do projeto (ConexaoBanco.cs).
// EN: The connection string comes from appsettings.json (local environment) or
//     from an environment variable / User Secrets — it's NEVER hardcoded in
//     source, unlike the original project's ConexaoBanco.cs.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// PT: Regras de negócio extraídas dos controllers (check-in/check-out, consumo,
//     confirmação de reserva) — ver pasta Services/.
// EN: Business rules extracted from the controllers (check-in/check-out,
//     consumption, booking confirmation) — see the Services/ folder.
builder.Services.AddScoped<IHospedagemService, HospedagemService>();
builder.Services.AddScoped<IConsumoService, ConsumoService>();
builder.Services.AddScoped<IReservaService, ReservaService>();
builder.Services.AddScoped<IAutenticacaoService, AutenticacaoService>();

// PT: Envio real de e-mail (Gmail via SMTP, ver Services/SmtpEmailSender.cs) —
//     usado pelo fluxo de "esqueci minha senha" (ContaController). Configuração
//     (Email:Remetente/Email:SenhaApp) vem de appsettings/variável de ambiente,
//     nunca hardcoded. Nos testes de integração, CustomWebApplicationFactory
//     substitui por uma fake que só captura o e-mail em memória.
// EN: Real email sending (Gmail via SMTP, see Services/SmtpEmailSender.cs) —
//     used by the "forgot my password" flow (ContaController). Configuration
//     (Email:Remetente/Email:SenhaApp) comes from appsettings/environment
//     variable, never hardcoded. In integration tests, CustomWebApplicationFactory
//     swaps it for a fake that just captures the email in memory.
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddControllersWithViews();

// PT: UserManager/SignInManager do ASP.NET Core Identity para as duas identidades,
//     usando EntityUserStore<T> (ver Authentication/EntityUserStore.cs) em vez do
//     UserStore pronto do EF Core — assim Funcionario/Hospede continuam sendo as
//     mesmas classes de sempre (FuncionarioId/HospedeId/Login/SenhaHash), sem herdar
//     de IdentityUser<T> e sem precisar de tabelas de apoio (UserClaims/UserRoles/
//     UserTokens) duplicadas por identidade. AddIdentityCore (não AddIdentity nem
//     AddDefaultIdentity) porque essas variantes tentam registrar cookie/esquema
//     próprio — os dois AddCookie(...) abaixo continuam sendo os únicos esquemas de
//     autenticação da aplicação; o Identity aqui só cuida de hash de senha, contagem
//     de tentativas falhas/bloqueio e geração/validação de token de redefinição de
//     senha (ver AutenticacaoService).
// EN: ASP.NET Core Identity's UserManager/SignInManager for both identities, using
//     EntityUserStore<T> (see Authentication/EntityUserStore.cs) instead of EF Core's
//     ready-made UserStore — so Funcionario/Hospede stay the same classes they always
//     were (FuncionarioId/HospedeId/Login/SenhaHash), with no IdentityUser<T>
//     inheritance and no per-identity duplicate support tables (UserClaims/UserRoles/
//     UserTokens). AddIdentityCore (not AddIdentity or AddDefaultIdentity) because
//     those variants try to register their own cookie/scheme — the two AddCookie(...)
//     calls below remain the app's only authentication schemes; Identity here only
//     handles password hashing, failed-attempt counting/lockout, and password-reset
//     token generation/validation (see AutenticacaoService).
void ConfigurarIdentity(IdentityOptions options)
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}

builder.Services.AddIdentityCore<Funcionario>(ConfigurarIdentity)
    .AddUserStore<EntityUserStore<Funcionario>>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddIdentityCore<Hospede>(ConfigurarIdentity)
    .AddUserStore<EntityUserStore<Hospede>>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// PT: Dois esquemas de cookie de autenticação independentes — nenhum é "padrão"
//     (ver Authentication/Esquemas.cs). Substituem a sessão manual: login vira
//     HttpContext.SignInAsync(esquema, claims), logout vira SignOutAsync, e a
//     proteção de rotas usa [Authorize]/[RequerFuncionario]/[RequerCargo] em vez de
//     checar Session.GetInt32(...) manualmente em cada controller.
// EN: Two independent cookie authentication schemes — neither is the "default"
//     (see Authentication/Esquemas.cs). They replace manual session handling: login
//     becomes HttpContext.SignInAsync(scheme, claims), logout becomes SignOutAsync,
//     and route protection uses [Authorize]/[RequerFuncionario]/[RequerCargo] instead
//     of manually checking Session.GetInt32(...) in every controller.
builder.Services.AddAuthentication()
    .AddCookie(Esquemas.Funcionario, options =>
    {
        options.LoginPath = "/Home/Login";
        options.Cookie.Name = "SisHospedar.Funcionario";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        // PT: Sem isso, o handler de cookie redireciona (302) pra uma AccessDeniedPath
        //     que não existe quando o cargo é insuficiente. Um 403 puro é o
        //     comportamento correto aqui (e o que os testes de autorização esperam).
        // EN: Without this, the cookie handler redirects (302) to an AccessDeniedPath
        //     that doesn't exist when the role is insufficient. A bare 403 is the
        //     correct behavior here (and what the authorization tests expect).
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    })
    .AddCookie(Esquemas.Hospede, options =>
    {
        options.LoginPath = "/Cliente/Login";
        options.Cookie.Name = "SisHospedar.Hospede";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// PT: Limite de tentativas por IP nos formulários públicos mais visados (login,
//     registro, esqueci senha) — usa o middleware nativo do ASP.NET Core
//     (System.Threading.RateLimiting), sem serviço externo e sem custo. Isso é
//     complementar ao lockout por conta do Identity (ConfigurarIdentity acima):
//     o lockout trava UMA conta específica após tentativas erradas nela, mas não
//     impede alguém de varrer várias contas diferentes a partir do mesmo IP — é
//     exatamente essa lacuna que o rate limiting por IP fecha.
// EN: Per-IP attempt limit on the most-targeted public forms (login, registration,
//     forgot password) — uses ASP.NET Core's built-in middleware
//     (System.Threading.RateLimiting), no external service, no cost. This is
//     complementary to Identity's per-account lockout (ConfigurarIdentity above):
//     lockout locks ONE specific account after bad attempts against it, but
//     doesn't stop someone from sweeping many different accounts from the same
//     IP — that's exactly the gap per-IP rate limiting closes.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Muitas tentativas seguidas. Aguarde alguns minutos e tente novamente.", ct);
    };

    static string ChaveIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

    // PT: Login e "esqueci minha senha" — 10 tentativas a cada 5 minutos por IP.
    // EN: Login and "forgot password" — 10 attempts every 5 minutes per IP.
    options.AddPolicy("AutenticacaoPublica", http => RateLimitPartition.GetFixedWindowLimiter(
        ChaveIp(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        }));

    // PT: Cadastro de hóspede — 5 contas a cada 1 hora por IP, contra spam de
    //     registro automatizado.
    // EN: Guest registration — 5 accounts per hour per IP, against automated
    //     registration spam.
    options.AddPolicy("CadastroPublico", http => RateLimitPartition.GetFixedWindowLimiter(
        ChaveIp(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

// PT: Popula o banco com dados de teste na primeira vez que rodar
//     (só insere se as tabelas estiverem vazias — não duplica em execuções seguintes).
// EN: Seeds the database with sample data on first run
//     (only inserts if the tables are empty — won't duplicate on later runs).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.Hospedes.Any())
    {
        db.Hospedes.AddRange(
            new Hospede { Nome = "Ana Beatriz Souza", Cpf = "123.456.789-01", Telefone = "(41) 99111-2233", DataNascimento = new DateTime(1990, 3, 14) },
            new Hospede { Nome = "Carlos Eduardo Lima", Cpf = "234.567.890-12", Telefone = "(41) 98222-3344", DataNascimento = new DateTime(1985, 7, 22) },
            new Hospede { Nome = "Fernanda Alves Costa", Cpf = "345.678.901-23", Telefone = "(41) 97333-4455", DataNascimento = new DateTime(1993, 11, 5) },
            new Hospede { Nome = "Gustavo Ribeiro Santos", Cpf = "456.789.012-34", Telefone = "(41) 96444-5566", DataNascimento = new DateTime(1988, 1, 30) },
            new Hospede { Nome = "Juliana Pereira Rocha", Cpf = "567.890.123-45", Telefone = "(41) 95555-6677", DataNascimento = new DateTime(1995, 9, 18) },
            new Hospede { Nome = "Marcelo Henrique Dias", Cpf = "678.901.234-56", Telefone = "(41) 94666-7788", DataNascimento = new DateTime(1979, 5, 2) }
        );
    }

    if (!db.Apartamentos.Any())
    {
        // PT: Numeração única "01".."10" (não mais 101/201/Suite-01) — 2 Standard, 4
        //     Executivo, 4 Master, nessa ordem.
        // EN: Single "01".."10" numbering (no more 101/201/Suite-01) — 2 Standard, 4
        //     Executive, 4 Master, in that order.
        db.Apartamentos.AddRange(
            // Standard (1 cama de solteiro) — R$100/diária
            new Apartamento { Numero = "01", Capacidade = 1, ValorDiaria = 100.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "02", Capacidade = 1, ValorDiaria = 100.00m, Status = StatusApartamento.Disponivel },
            // Executivo (1 cama de casal) — R$200/diária
            new Apartamento { Numero = "03", Capacidade = 2, ValorDiaria = 200.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "04", Capacidade = 2, ValorDiaria = 200.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "05", Capacidade = 2, ValorDiaria = 200.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "06", Capacidade = 2, ValorDiaria = 200.00m, Status = StatusApartamento.Disponivel },
            // Master (1 cama de casal + 1 de solteiro) — R$300/diária
            new Apartamento { Numero = "07", Capacidade = 3, ValorDiaria = 300.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "08", Capacidade = 3, ValorDiaria = 300.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "09", Capacidade = 3, ValorDiaria = 300.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "10", Capacidade = 3, ValorDiaria = 300.00m, Status = StatusApartamento.Disponivel }
        );
    }

    if (!db.Produtos.Any())
    {
        db.Produtos.AddRange(
            new Produto { Nome = "Água mineral 500ml", Preco = 6.00m, Estoque = 100 },
            new Produto { Nome = "Refrigerante lata", Preco = 8.00m, Estoque = 80 },
            new Produto { Nome = "Cerveja long neck", Preco = 12.00m, Estoque = 60 },
            new Produto { Nome = "Sanduíche natural", Preco = 22.00m, Estoque = 30 },
            new Produto { Nome = "Barra de chocolate", Preco = 9.50m, Estoque = 50 }
        );
    }

    // PT: Funcionários de teste — login e senha para você testar cada painel.
    //     SENHA PARA TODOS: Senha123
    // EN: Test employees — login and password to try out each dashboard.
    //     PASSWORD FOR ALL: Senha123
    if (!db.Funcionarios.Any())
    {
        var hasher = new PasswordHasher<Funcionario>();

        // PT: Login = primeiro nome + "." + sobrenome, sempre gerado pelo sistema — é
        //     exatamente o que FuncionarioController.Create faz ao cadastrar um novo
        //     funcionário (só o hóspede escolhe o próprio login, no autocadastro).
        // EN: Login = first name + "." + last name, always system-generated — this is
        //     exactly what FuncionarioController.Create does when registering a new
        //     employee (only the guest picks their own login, at self-registration).
        var funcionariosTeste = new[]
        {
            new Funcionario { Nome = "Wesley Sales", Cargo = CargoFuncionario.Administrador, Login = "wesley.sales" },
            new Funcionario { Nome = "Luan Santos", Cargo = CargoFuncionario.Gerente, Login = "luan.santos" },
            new Funcionario { Nome = "Matias Sampaio", Cargo = CargoFuncionario.Recepcionista, Login = "matias.sampaio" },
            new Funcionario { Nome = "Camila Rocha", Cargo = CargoFuncionario.Limpeza, Login = "camila.rocha" },
            new Funcionario { Nome = "Diego Fernandes", Cargo = CargoFuncionario.Vallet, Login = "diego.fernandes" },
        };

        // PT: SecurityStamp precisa existir mesmo pra contas criadas fora do
        //     UserManager (como este seed) — o Identity lança exceção ao validar um
        //     usuário sem stamp (ex.: ao contar uma tentativa de login errada). Contas
        //     criadas pelo fluxo normal (FuncionarioController.Create,
        //     ClienteController.Registrar) já ganham o stamp automaticamente via
        //     UserManager.CreateAsync.
        // EN: SecurityStamp must exist even for accounts created outside UserManager
        //     (like this seed) — Identity throws when validating a user with no stamp
        //     (e.g. while counting a failed login attempt). Accounts created through
        //     the normal flow (FuncionarioController.Create, ClienteController.Registrar)
        //     already get their stamp automatically via UserManager.CreateAsync.
        foreach (var f in funcionariosTeste)
        {
            f.SenhaHash = hasher.HashPassword(f, "Senha123");
            f.SecurityStamp = Guid.NewGuid().ToString();
        }

        db.Funcionarios.AddRange(funcionariosTeste);
    }

    db.SaveChanges();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// PT: Desativado só nos testes de integração (ver CustomWebApplicationFactory) —
//     com 24 testes reaproveitando o mesmo host de teste (IClassFixture), várias
//     dezenas de POSTs em /Home/Login, /Cliente/Login, /Cliente/Registrar e
//     /Conta/EsqueciSenha ao longo da suíte esbarrariam num limite pensado para
//     uso real (por IP, minutos), gerando falhas 429 sem relação com o que cada
//     teste realmente verifica. Em produção, a variável nunca é definida e o
//     rate limiting continua ativo normalmente.
// EN: Disabled only for integration tests (see CustomWebApplicationFactory) —
//     with 24 tests reusing the same test host (IClassFixture), dozens of POSTs
//     to /Home/Login, /Cliente/Login, /Cliente/Registrar and /Conta/EsqueciSenha
//     across the suite would hit a limit designed for real-world use (per IP,
//     minutes), causing 429 failures unrelated to what each test actually
//     checks. In production, the variable is never set and rate limiting stays
//     active normally.
if (app.Configuration["DesativarRateLimiting"] != "true") app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// PT: A classe Program gerada pelos top-level statements é internal por padrão; esta
//     declaração parcial a torna public para que SisHospedar.Tests possa usar
//     WebApplicationFactory<Program> nos testes de integração.
// EN: The Program class generated from top-level statements is internal by default;
//     this partial declaration makes it public so SisHospedar.Tests can use
//     WebApplicationFactory<Program> in integration tests.
public partial class Program { }
