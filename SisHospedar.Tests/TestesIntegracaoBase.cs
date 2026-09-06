using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SisHospedar.Tests;

// PT: Infraestrutura comum aos testes de integração: cliente HTTP sem auto-redirect
//     (pra inspecionar 302/403 diretamente), login real de funcionário via HTTP, e
//     envio de formulários com o token de antiforgery — igual a um form real faria.
// EN: Shared infrastructure for integration tests: an HTTP client with no auto-redirect
//     (to inspect 302/403 directly), a real employee login over HTTP, and form
//     submission with the antiforgery token — the same way a real form would.
public abstract class TestesIntegracaoBase : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory Factory;

    protected TestesIntegracaoBase(CustomWebApplicationFactory factory) => Factory = factory;

    protected HttpClient CriarCliente() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected static async Task<string> ExtrairAntiforgeryTokenAsync(HttpResponseMessage resposta)
    {
        var html = await resposta.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        return match.Groups[1].Value;
    }

    protected async Task LoginComoAsync(HttpClient client, string login, string senha)
    {
        var paginaLogin = await client.GetAsync("/Home/Login");
        var token = await ExtrairAntiforgeryTokenAsync(paginaLogin);

        var resposta = await client.PostAsync("/Home/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["login"] = login,
                ["senha"] = senha,
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
    }

    // PT: Busca o token de antiforgery numa página GET qualquer (não precisa ter o form
    //     que será enviado, só precisa ter o cookie+token no ar) e envia o POST com os
    //     campos informados + o token, exatamente como um <form> real faria.
    // EN: Grabs the antiforgery token from any GET page (it doesn't need to contain the
    //     form being submitted, just needs the cookie+token to be issued) and posts the
    //     given fields + the token, exactly like a real <form> would.
    protected async Task<HttpResponseMessage> PostComAntiforgeryAsync(
        HttpClient client, string urlParaObterToken, string urlPost, Dictionary<string, string> campos)
    {
        var pagina = await client.GetAsync(urlParaObterToken);
        var token = await ExtrairAntiforgeryTokenAsync(pagina);
        campos["__RequestVerificationToken"] = token;
        return await client.PostAsync(urlPost, new FormUrlEncodedContent(campos));
    }
}
