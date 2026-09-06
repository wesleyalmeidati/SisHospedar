using System.Net;

namespace SisHospedar.Tests;

// PT: Testes de integração que validam a proteção das rotas de funcionário — os mesmos
//     cenários verificados manualmente com curl logo após a refatoração dos filtros de
//     autorização (RequerFuncionarioAttribute / RequerCargoAttribute), agora automatizados.
// EN: Integration tests validating employee-route protection — the same scenarios
//     manually verified with curl right after refactoring the authorization filters
//     (RequerFuncionarioAttribute / RequerCargoAttribute), now automated.
public class AutorizacaoFuncionarioTests : TestesIntegracaoBase
{
    public AutorizacaoFuncionarioTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Theory]
    [InlineData("/Hospedagem")]
    [InlineData("/Apartamento")]
    [InlineData("/Produto")]
    [InlineData("/Hospede")]
    [InlineData("/Funcionario")]
    [InlineData("/Painel")]
    [InlineData("/Reserva")]
    [InlineData("/Consumo/Create")]
    public async Task AnonimoEmRotaDeFuncionario_RedirecionaParaLogin(string rota)
    {
        var client = CriarCliente();

        var resposta = await client.GetAsync(rota);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/Home/Login", resposta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Recepcionista_AcessaHospedagem_MasNaoAcessaFuncionario()
    {
        var client = CriarCliente();
        await LoginComoAsync(client, "recepcao", "Senha123");

        var hospedagem = await client.GetAsync("/Hospedagem");
        Assert.Equal(HttpStatusCode.OK, hospedagem.StatusCode);

        var funcionario = await client.GetAsync("/Funcionario");
        Assert.Equal(HttpStatusCode.Forbidden, funcionario.StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("gerente")]
    public async Task AdministradorOuGerente_AcessaFuncionario(string login)
    {
        var client = CriarCliente();
        await LoginComoAsync(client, login, "Senha123");

        var resposta = await client.GetAsync("/Funcionario");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task LoginComSenhaErrada_NaoAbreASessao()
    {
        var client = CriarCliente();

        var resposta = await PostComAntiforgeryAsync(client, "/Home/Login", "/Home/Login",
            new Dictionary<string, string> { ["login"] = "admin", ["senha"] = "senha-errada" });

        // PT: Login inválido apenas re-renderiza a view (200), não redireciona.
        // EN: Invalid login just re-renders the view (200), it doesn't redirect.
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var painel = await client.GetAsync("/Painel");
        Assert.Equal(HttpStatusCode.Redirect, painel.StatusCode);
    }

    [Fact]
    public async Task Funcionario_AcessandoReservaCreateDoHospede_RedirecionaParaLoginDeCliente()
    {
        // PT: /Reserva/Create pertence à identidade de hóspede, não à de funcionário —
        //     mesmo controller, filtro aplicado por método, não pela classe.
        // EN: /Reserva/Create belongs to the guest identity, not the employee one —
        //     same controller, filter applied per-method, not on the class.
        var client = CriarCliente();
        await LoginComoAsync(client, "recepcao", "Senha123");

        var resposta = await client.GetAsync("/Reserva/Create");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/Cliente/Login", resposta.Headers.Location?.OriginalString);
    }
}
