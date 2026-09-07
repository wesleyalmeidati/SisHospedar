using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Authentication;
using SisHospedar.Data;
using SisHospedar.Models;
using SisHospedar.Services;

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

builder.Services.AddControllersWithViews();

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

        foreach (var f in funcionariosTeste)
            f.SenhaHash = hasher.HashPassword(f, "Senha123");

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
