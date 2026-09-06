using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SisHospedar.Data;

namespace SisHospedar.Tests;

// PT: Sobe a aplicação real (Program.cs) em memória para testes de integração, trocando
//     o SqlServer configurado lá por um banco EF Core InMemory isolado por instância —
//     assim os testes rodam sem precisar de SQL Server e sem tocar no banco de dev.
//     A seed de dados do próprio Program.cs (funcionários de teste, hóspedes, etc.) roda
//     normalmente contra esse banco em memória, então os testes podem reusar os logins
//     conhecidos (admin/gerente/recepcao/limpeza/vallet, senha "Senha123").
// EN: Boots the real application (Program.cs) in memory for integration tests, swapping
//     the SqlServer configured there for an EF Core InMemory database isolated per
//     instance — so tests run without SQL Server and without touching the dev database.
//     Program.cs's own seed data (test employees, guests, etc.) runs normally against
//     this in-memory database, so tests can reuse the known logins
//     (admin/gerente/recepcao/limpeza/vallet, password "Senha123").
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    static CustomWebApplicationFactory()
    {
        // PT: Program.cs lança exceção se não houver connection string configurada.
        //     Esse valor nunca chega a ser usado de fato — o DbContext é substituído
        //     por InMemory logo abaixo, antes de qualquer acesso real ao banco.
        // EN: Program.cs throws if no connection string is configured. This value is
        //     never actually used — the DbContext is swapped for InMemory right below,
        //     before any real database access happens.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Server=(test);Database=SisHospedarTest;Trusted_Connection=True;");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));
        });
    }
}
