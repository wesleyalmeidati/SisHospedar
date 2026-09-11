using System.Net;
using SisHospedar.Models;

namespace SisHospedar.Tests;

// PT: Estes testes existem por causa de um erro real: o painel de Administrador/Gerente foi
//     para produção lançando 500. A consulta de audiência projetava direto num record e
//     ordenava por uma propriedade dele, o que o EF Core não traduz — e isso só estoura em
//     tempo de execução, nunca na compilação.
//     A lacuna não era a consulta em si, era a cobertura: os testes de autorização abriam o
//     /Painel com cargos que não passam pelo ramo de gestão, então ninguém exercitava o
//     caminho quebrado. Abrir o painel de cada cargo e exigir 200 é barato e pega qualquer
//     consulta intraduzível que alguém acrescente ali depois.
// EN: These tests exist because of a real mistake: the Administrador/Gerente dashboard
//     shipped to production throwing a 500. The audience query projected straight into a
//     record and ordered by one of its properties, which EF Core can't translate — and that
//     only blows up at runtime, never at compile time.
//     The gap wasn't the query itself, it was coverage: the authorization tests opened
//     /Painel with roles that don't go through the management branch, so nobody exercised
//     the broken path. Opening the dashboard for every role and requiring a 200 is cheap and
//     catches any untranslatable query added there later.
public class PainelTests : TestesIntegracaoBase
{
    public PainelTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("wesley.sales")]    // Administrador
    [InlineData("luan.santos")]     // Gerente
    [InlineData("matias.sampaio")]  // Recepcionista
    [InlineData("camila.rocha")]    // Limpeza
    [InlineData("diego.fernandes")] // Vallet
    public async Task Painel_AbreParaTodosOsCargos(string login)
    {
        var client = CriarCliente();
        await LoginComoAsync(client, login, "Senha123");

        var resposta = await client.GetAsync("/Painel");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Painel_DeGerente_MostraAudienciaComDadosGravados()
    {
        // PT: Com linhas reais em VisitasPagina a consulta agregada roda de verdade —
        //     um painel vazio poderia passar sem nunca executar o GroupBy.
        // EN: With real rows in VisitasPagina the aggregate query actually runs — an empty
        //     dashboard could pass without ever executing the GroupBy.
        await Factory.UsandoContextoAsync(async db =>
        {
            db.VisitasPagina.AddRange(
                new VisitaPagina { Caminho = "/", Data = DateTime.Today, Total = 12, TotalRecorrentes = 4 },
                new VisitaPagina { Caminho = "/Privacidade", Data = DateTime.Today, Total = 3, TotalRecorrentes = 0 });
            await db.SaveChangesAsync();
        });

        var client = CriarCliente();
        await LoginComoAsync(client, "luan.santos", "Senha123");

        var html = await client.GetStringAsync("/Painel");

        Assert.Contains("Audiência do site", html);
        Assert.Contains("/Privacidade", html);
    }
}
