using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SisHospedar.Tests;

// PT: Testes de integração que validam a proteção das rotas de funcionário — os mesmos
//     cenários verificados manualmente com curl logo após a refatoração dos filtros de
//     autorização (RequerFuncionarioAttribute / RequerCargoAttribute), agora automatizados.
// EN: Integration tests validating employee-route protection — the same scenarios
//     manually verified with curl right after refactoring the authorization filters
//     (RequerFuncionarioAttribute / RequerCargoAttribute), now automated.
public class AutorizacaoFuncionarioTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AutorizacaoFuncionarioTests(CustomWebApplicationFactory factory) => _factory = factory;

    private HttpClient CriarCliente() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> ExtrairAntiforgeryTokenAsync(HttpResponseMessage resposta)
    {
        var html = await resposta.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        return match.Groups[1].Value;
    }

    // PT: Reproduz o fluxo real de login via HTTP (GET pega o token/cookie de
    //     antiforgery, POST envia as credenciais) — igual ao que foi validado com curl.
    // EN: Reproduces the real HTTP login flow (GET grabs the antiforgery token/cookie,
    //     POST sends the credentials) — same as what was validated with curl.
    private async Task LoginComoAsync(HttpClient client, string login, string senha)
    {
        var paginaLogin = await client.GetAsync("/Home/Login");
        var token = await ExtrairAntiforgeryTokenAsync(paginaLogin);

        var form = new Dictionary<string, string>
        {
            ["login"] = login,
            ["senha"] = senha,
            ["__RequestVerificationToken"] = token
        };

        var resposta = await client.PostAsync("/Home/Login", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
    }

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

        var paginaLogin = await client.GetAsync("/Home/Login");
        var token = await ExtrairAntiforgeryTokenAsync(paginaLogin);

        var resposta = await client.PostAsync("/Home/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["login"] = "admin",
                ["senha"] = "senha-errada",
                ["__RequestVerificationToken"] = token
            }));

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
