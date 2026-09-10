using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
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

// PT: Em produção a aplicação roda atrás do ingress do Azure Container Apps, que
//     termina o TLS e repassa a requisição pro Kestrel em HTTP puro, informando o
//     protocolo/IP originais em X-Forwarded-Proto/X-Forwarded-For. Sem ler esses
//     headers, a aplicação se enxerga em HTTP e três coisas quebram em silêncio:
//     o cookie de autenticação sai sem a flag Secure (CookieSecurePolicy padrão é
//     SameAsRequest), o UseHsts() nunca emite o header (só emite em requisição
//     HTTPS) e os redirects saem com "http://". Pior: RemoteIpAddress vira o IP
//     interno do ingress, igual pra todo mundo — o que torna o rate limiting
//     "por IP" abaixo um limite GLOBAL, compartilhado por todos os visitantes.
//     KnownNetworks/KnownProxies são limpos porque o IP do ingress não é fixo nem
//     conhecido de antemão; isso é seguro aqui porque o container só é alcançável
//     através do ingress, nunca diretamente.
// EN: In production the app runs behind the Azure Container Apps ingress, which
//     terminates TLS and forwards the request to Kestrel over plain HTTP, passing
//     the original scheme/IP in X-Forwarded-Proto/X-Forwarded-For. Without reading
//     those headers the app believes it's on HTTP and three things break silently:
//     the auth cookie ships without the Secure flag (default CookieSecurePolicy is
//     SameAsRequest), UseHsts() never emits its header (HTTPS requests only), and
//     redirects are written with "http://". Worse: RemoteIpAddress becomes the
//     ingress's internal IP, identical for everyone — which turns the "per IP"
//     rate limiting below into a GLOBAL limit shared by every visitor.
//     KnownNetworks/KnownProxies are cleared because the ingress IP is neither
//     fixed nor known upfront; that's safe here because the container is only
//     reachable through the ingress, never directly.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// PT: Compressão de resposta (Brotli/Gzip por padrão). EnableForHttps é false por
//     padrão no ASP.NET Core por causa do ataque BREACH, que explora compressão +
//     segredo refletido na mesma resposta; aqui o segredo em jogo seria o token de
//     antiforgery, que o ASP.NET Core já mascara com um valor aleatório por
//     resposta — justamente a mitigação recomendada contra BREACH. Como todo o
//     tráfego é HTTPS, sem isso não haveria compressão nenhuma.
// EN: Response compression (Brotli/Gzip by default). EnableForHttps defaults to
//     false in ASP.NET Core because of the BREACH attack, which exploits
//     compression + a secret reflected in the same response; the secret here would
//     be the antiforgery token, which ASP.NET Core already masks with a per-response
//     random value — precisely the recommended BREACH mitigation. Since all traffic
//     is HTTPS, without this there would be no compression at all.
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

// PT: O cookie de antiforgery é o único que o ASP.NET Core cria com SecurePolicy
//     "None" por padrão (para funcionar em dev sobre HTTP puro). SameAsRequest o
//     alinha aos cookies de autenticação: com o UseForwardedHeaders acima, em
//     produção a requisição é HTTPS e ele passa a sair com a flag Secure, enquanto
//     localmente sobre HTTP continua funcionando normalmente.
// EN: The antiforgery cookie is the only one ASP.NET Core creates with SecurePolicy
//     "None" by default (so it works in dev over plain HTTP). SameAsRequest aligns it
//     with the authentication cookies: with UseForwardedHeaders above, the request is
//     HTTPS in production and the cookie starts shipping with the Secure flag, while
//     locally over HTTP it keeps working normally.
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);

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
        // PT: Dígitos verificadores calculados de verdade (algoritmo módulo 11, ver
        //     CpfValidoAttribute) — não são CPFs reais de ninguém, só números
        //     sintéticos que passam no mesmo cálculo que a validação nova exige.
        //     Precisou trocar porque os valores antigos (terminados em -01, -12...)
        //     eram só um padrão sequencial que nunca seria um CPF válido de verdade;
        //     com o CpfValidoAttribute agora aplicado em Hospede.Cpf, editar um
        //     desses hóspedes sem tocar no CPF passaria a falhar a validação no re-save.
        // EN: Real, calculated check digits (modulo-11 algorithm, see
        //     CpfValidoAttribute) — not anyone's real CPF, just synthetic numbers
        //     that pass the same math the new validation requires. Had to change
        //     because the old values (ending in -01, -12...) were just a sequential
        //     pattern that could never be a genuinely valid CPF; with
        //     CpfValidoAttribute now applied to Hospede.Cpf, editing one of these
        //     guests without touching the CPF would start failing validation on re-save.
        db.Hospedes.AddRange(
            new Hospede { Nome = "Ana Beatriz Souza", Cpf = "123.456.789-09", Telefone = "(41) 99111-2233", DataNascimento = new DateTime(1990, 3, 14) },
            new Hospede { Nome = "Carlos Eduardo Lima", Cpf = "234.567.890-92", Telefone = "(41) 98222-3344", DataNascimento = new DateTime(1985, 7, 22) },
            new Hospede { Nome = "Fernanda Alves Costa", Cpf = "345.678.901-75", Telefone = "(41) 97333-4455", DataNascimento = new DateTime(1993, 11, 5) },
            new Hospede { Nome = "Gustavo Ribeiro Santos", Cpf = "456.789.012-49", Telefone = "(41) 96444-5566", DataNascimento = new DateTime(1988, 1, 30) },
            new Hospede { Nome = "Juliana Pereira Rocha", Cpf = "567.890.123-03", Telefone = "(41) 95555-6677", DataNascimento = new DateTime(1995, 9, 18) },
            new Hospede { Nome = "Marcelo Henrique Dias", Cpf = "678.901.234-69", Telefone = "(41) 94666-7788", DataNascimento = new DateTime(1979, 5, 2) }
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

// PT: Precisa ser o PRIMEIRO middleware — tudo que depende de esquema (HSTS,
//     redirect HTTPS, flag Secure do cookie) ou de IP de origem (rate limiting)
//     lê esses valores depois daqui. Ver a configuração de ForwardedHeadersOptions.
// EN: Must be the FIRST middleware — everything that depends on the scheme (HSTS,
//     HTTPS redirect, the cookie's Secure flag) or on the client IP (rate limiting)
//     reads those values after this point. See the ForwardedHeadersOptions setup.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// PT: Antes do UseStaticFiles pra que o CSS também seja comprimido.
// EN: Before UseStaticFiles so the CSS gets compressed too.
app.UseResponseCompression();

// PT: Cabeçalhos de segurança. frame-ancestors 'none' (com X-Frame-Options junto,
//     para navegadores antigos) impede que o painel seja embutido num iframe de
//     outro site — clickjacking. A lista de origens da CSP é exatamente o que as
//     Views e o site.css já carregam hoje: Bootstrap via jsDelivr, fontes do Google
//     (o CSS vem de fonts.googleapis.com e os arquivos de fonte de fonts.gstatic.com)
//     e as fotos de Unsplash/Pexels. 'unsafe-inline' em style-src é necessário
//     porque algumas Views usam atributo style="..." direto no elemento.
// EN: Security headers. frame-ancestors 'none' (plus X-Frame-Options for older
//     browsers) stops the dashboard from being embedded in another site's iframe —
//     clickjacking. The CSP origin list is exactly what the Views and site.css
//     already load today: Bootstrap via jsDelivr, Google Fonts (the CSS comes from
//     fonts.googleapis.com and the font files from fonts.gstatic.com) and the
//     Unsplash/Pexels photos. 'unsafe-inline' in style-src is required because some
//     Views use a style="..." attribute directly on the element.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https://images.unsplash.com https://images.pexels.com; " +
        "media-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

// PT: Sem Cache-Control, o navegador revalida cada arquivo estático a cada
//     navegação — um round-trip inteiro (~270ms daqui até o leste dos EUA) só pra
//     receber "304 Não Modificado". O site.css é referenciado com
//     asp-append-version="true" no _Layout, então a URL muda junto com o conteúdo
//     e um cache longo nunca serve arquivo velho.
// EN: With no Cache-Control the browser revalidates every static file on each
//     navigation — a full round-trip (~270ms from here to US East) just to get back
//     "304 Not Modified". site.css is referenced with asp-append-version="true" in
//     _Layout, so its URL changes together with its content and a long cache can
//     never serve a stale file.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.CacheControl = "public,max-age=604800"
});

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
