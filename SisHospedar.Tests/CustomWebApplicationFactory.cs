using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SisHospedar.Data;
using SisHospedar.Services;

namespace SisHospedar.Tests;

// PT: Sobe a aplicação real (Program.cs) em memória para testes de integração, trocando
//     o SqlServer configurado lá por SQLite em memória — um banco relacional de verdade,
//     ao contrário do provider "InMemory" do EF Core, que não sabe traduzir operações
//     como ExecuteUpdateAsync (usado no decremento atômico de estoque em ConsumoController).
//     A seed de dados do próprio Program.cs (funcionários de teste, hóspedes, etc.) roda
//     normalmente contra esse banco, então os testes podem reusar os logins conhecidos
//     (admin/gerente/recepcao/limpeza/vallet, senha "Senha123").
// EN: Boots the real application (Program.cs) in memory for integration tests, swapping
//     the SqlServer configured there for in-memory SQLite — a real relational database,
//     unlike EF Core's "InMemory" provider, which can't translate operations like
//     ExecuteUpdateAsync (used for the atomic stock decrement in ConsumoController).
//     Program.cs's own seed data (test employees, guests, etc.) runs normally against
//     this database, so tests can reuse the known logins
//     (admin/gerente/recepcao/limpeza/vallet, password "Senha123").
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // PT: Uma conexão SQLite ":memory:" só existe enquanto a conexão que a abriu fica
    //     aberta — por isso ela é mantida como campo e só fechada no Dispose da factory.
    // EN: A SQLite ":memory:" database only exists while the connection that opened it
    //     stays open — that's why it's kept as a field and only closed in the factory's Dispose.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    // PT: Instância única compartilhada por toda a factory — substitui
    //     SmtpEmailSender (que precisaria de credenciais SMTP reais) para que os
    //     testes de redefinição de senha consigam recuperar o link "enviado".
    // EN: Single instance shared across the whole factory — replaces
    //     SmtpEmailSender (which would need real SMTP credentials) so
    //     password-reset tests can retrieve the "sent" link.
    public FakeEmailSender EmailSender { get; } = new();

    static CustomWebApplicationFactory()
    {
        // PT: Program.cs lança exceção se não houver connection string configurada.
        //     Esse valor nunca chega a ser usado de fato — o DbContext é substituído
        //     por SQLite logo abaixo, antes de qualquer acesso real ao banco.
        // EN: Program.cs throws if no connection string is configured. This value is
        //     never actually used — the DbContext is swapped for SQLite right below,
        //     before any real database access happens.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Server=(test);Database=SisHospedarTest;Trusted_Connection=True;");

        // PT: Ver o comentário em Program.cs, perto de "app.UseRateLimiter()" — sem
        //     isso, a soma de POSTs em endpoints públicos ao longo de toda a suíte
        //     estouraria o limite por IP (pensado para uso real) e causaria 429
        //     inesperados, sem relação com o comportamento que cada teste verifica.
        // EN: See the comment in Program.cs, near "app.UseRateLimiter()" — without
        //     this, the sum of POSTs to public endpoints across the whole suite
        //     would blow past the per-IP limit (designed for real-world use) and
        //     cause unexpected 429s, unrelated to the behavior each test checks.
        Environment.SetEnvironmentVariable("DesativarRateLimiting", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailSender));
            if (emailDescriptor != null) services.Remove(emailDescriptor);
            services.AddSingleton<IEmailSender>(EmailSender);

            // PT: Cria o schema a partir do modelo atual (sem depender das migrations),
            //     antes do próprio Program.cs rodar sua seed de dados de teste.
            // EN: Creates the schema from the current model (no dependency on migrations),
            //     before Program.cs itself runs its test data seed.
            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }

    // PT: Dá aos testes acesso direto ao mesmo banco usado pela aplicação — para inserir
    //     dados de cenário (arrange) e conferir o estado final (assert) sem precisar
    //     passar tudo pela camada HTTP.
    // EN: Gives tests direct access to the same database the app uses — to insert
    //     scenario data (arrange) and check the resulting state (assert) without
    //     having to route everything through the HTTP layer.
    public async Task UsandoContextoAsync(Func<AppDbContext, Task> acao)
    {
        using var scope = Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await acao(contexto);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
