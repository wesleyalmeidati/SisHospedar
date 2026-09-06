using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
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
        db.Apartamentos.AddRange(
            new Apartamento { Numero = "101", Capacidade = 2, ValorDiaria = 180.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "102", Capacidade = 2, ValorDiaria = 180.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "201", Capacidade = 4, ValorDiaria = 320.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "202", Capacidade = 4, ValorDiaria = 320.00m, Status = StatusApartamento.Manutencao },
            new Apartamento { Numero = "301", Capacidade = 6, ValorDiaria = 480.00m, Status = StatusApartamento.Disponivel },
            new Apartamento { Numero = "Suite-01", Capacidade = 3, ValorDiaria = 650.00m, Status = StatusApartamento.Disponivel }
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

        var funcionariosTeste = new[]
        {
            new Funcionario { Nome = "Wesley Almeida (Admin)", Cargo = CargoFuncionario.Administrador, Login = "admin" },
            new Funcionario { Nome = "Gerente Solaris", Cargo = CargoFuncionario.Gerente, Login = "gerente" },
            new Funcionario { Nome = "Recepção Solaris", Cargo = CargoFuncionario.Recepcionista, Login = "recepcao" },
            new Funcionario { Nome = "Equipe de Limpeza", Cargo = CargoFuncionario.Limpeza, Login = "limpeza" },
            new Funcionario { Nome = "Equipe de Vallet", Cargo = CargoFuncionario.Vallet, Login = "vallet" },
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
app.UseSession();
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
