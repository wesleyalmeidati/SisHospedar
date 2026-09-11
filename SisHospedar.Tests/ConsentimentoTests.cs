using System.Net;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Services;

namespace SisHospedar.Tests;

// PT: O valor destes testes não é cobrir linhas — é impedir que o consentimento vire
//     enfeite. Um banner é fácil de manter bonito e fácil de quebrar por dentro: basta
//     alguém gravar o cookie de medição antes da resposta, ou "simplificar" a recusa para
//     só não gravar mais nada (deixando o identificador antigo no navegador). Nenhuma das
//     duas regressões aparece na tela. Estes testes verificam o comportamento observável
//     nos cookies da resposta HTTP, que é onde a promessa da política ou se cumpre ou não.
// EN: The value of these tests isn't line coverage — it's keeping consent from becoming
//     decoration. A banner is easy to keep looking right and easy to break underneath:
//     someone need only write the measurement cookie before the answer, or "simplify"
//     refusal into merely not writing anything new (leaving the old identifier in the
//     browser). Neither regression is visible on screen. These tests check the observable
//     behavior in the HTTP response cookies, which is where the policy's promise is either
//     kept or broken.
public class ConsentimentoTests : TestesIntegracaoBase
{
    public ConsentimentoTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static IEnumerable<string> CookiesDefinidos(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Set-Cookie", out var valores) ? valores : Array.Empty<string>();

    [Fact]
    public async Task SemDecisao_NaoGravaCookieDeMedicao()
    {
        var client = CriarCliente();

        var resposta = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.DoesNotContain(CookiesDefinidos(resposta), c => c.Contains(Medicao.CookieVisitante));
    }

    [Fact]
    public async Task SemDecisao_MostraOBannerComAceitarERecusar()
    {
        var client = CriarCliente();

        var html = await client.GetStringAsync("/");

        // PT: Os dois botões precisam existir na mesma tela. Esconder a recusa atrás de um
        //     segundo clique é o padrão escuro que a ANPD condena.
        // EN: Both buttons have to exist on the same screen. Hiding refusal behind a second
        //     click is the dark pattern the ANPD objects to.
        Assert.Contains("Aceitar", html);
        Assert.Contains("Recusar", html);
    }

    [Fact]
    public async Task AoRecusar_NaoGravaIdentificadorERegistraADecisao()
    {
        var client = CriarCliente();

        var resposta = await PostComAntiforgeryAsync(client, "/", "/Privacidade/Consentimento",
            new Dictionary<string, string> { ["aceito"] = "false" });

        var cookies = CookiesDefinidos(resposta).ToList();

        // A decisão fica gravada, senão o banner reapareceria a cada página.
        Assert.Contains(cookies, c => c.Contains(Medicao.CookieConsentimento) && c.Contains("recusado"));

        // PT: E o identificador precisa ser expirado, não apenas "não renovado" — quem
        //     revoga tem que ficar sem ele de fato.
        // EN: And the identifier must be expired, not merely "not renewed" — whoever
        //     revokes has to actually end up without it.
        Assert.Contains(cookies, c => c.Contains(Medicao.CookieVisitante) && c.Contains("expires=Thu, 01 Jan 1970"));

        await Factory.UsandoContextoAsync(async db =>
            Assert.True(await db.ConsentimentosCookie.AnyAsync(c => !c.Aceito)));
    }

    [Fact]
    public async Task AoAceitar_RegistraADecisaoComVersaoDaPolitica()
    {
        var client = CriarCliente();

        var resposta = await PostComAntiforgeryAsync(client, "/", "/Privacidade/Consentimento",
            new Dictionary<string, string> { ["aceito"] = "true" });

        Assert.Contains(CookiesDefinidos(resposta),
            c => c.Contains(Medicao.CookieConsentimento) && c.Contains("aceito"));

        await Factory.UsandoContextoAsync(async db =>
        {
            var registro = await db.ConsentimentosCookie
                .Where(c => c.Aceito)
                .OrderByDescending(c => c.ConsentimentoCookieId)
                .FirstOrDefaultAsync();

            Assert.NotNull(registro);
            Assert.Equal(Medicao.VersaoPolitica, registro!.VersaoPolitica);

            // PT: A prova de consentimento não pode virar cadastro de rastreamento: o
            //     registro guarda a decisão, não o visitante.
            // EN: The consent proof must not become a tracking record: it stores the
            //     decision, not the visitor.
            var propriedades = typeof(SisHospedar.Models.ConsentimentoCookie)
                .GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain("Ip", propriedades);
            Assert.DoesNotContain("UserAgent", propriedades);
            Assert.DoesNotContain("HospedeId", propriedades);
        });
    }

    [Fact]
    public async Task DepoisDeDecidir_OBannerNaoReaparece()
    {
        var client = CriarCliente();
        await PostComAntiforgeryAsync(client, "/", "/Privacidade/Consentimento",
            new Dictionary<string, string> { ["aceito"] = "false" });

        var html = await client.GetStringAsync("/");

        // PT: Continuar perguntando a quem já recusou é insistência até a pessoa ceder —
        //     exatamente o que a recusa deveria evitar.
        // EN: Continuing to ask someone who already refused is nagging until they give in —
        //     exactly what the refusal was supposed to prevent.
        Assert.DoesNotContain("Consentimento de cookies", html);
    }

    [Fact]
    public void Buffer_AcumulaEmMemoriaEZeraAoDrenar()
    {
        var buffer = new BufferVisitas();

        buffer.Registrar("/", recorrente: false);
        buffer.Registrar("/", recorrente: true);
        buffer.Registrar("/Privacidade", recorrente: false);

        var primeira = buffer.Drenar();

        var home = Assert.Single(primeira, v => v.Caminho == "/");
        Assert.Equal(2, home.Total);
        Assert.Equal(1, home.Recorrentes);
        Assert.Single(primeira, v => v.Caminho == "/Privacidade");

        // PT: Drenar tem que zerar: se devolvesse os mesmos valores de novo, cada ciclo do
        //     FlushVisitasService somaria tudo outra vez e a contagem cresceria sozinha.
        // EN: Draining must reset: if it returned the same values again, every
        //     FlushVisitasService cycle would add everything once more and the count would
        //     inflate on its own.
        Assert.Empty(buffer.Drenar());
    }
}
