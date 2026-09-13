using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Models;

namespace SisHospedar.Tests;

// PT: Testes de integração para a parte do ASP.NET Core Identity de verdade
//     (UserManager/SignInManager via EntityUserStore, ver Authentication/EntityUserStore.cs)
//     — bloqueio de conta após tentativas inválidas seguidas, e o fluxo completo de
//     redefinição de senha (pedido em /Conta/EsqueciSenha, link seguido até
//     /Conta/RedefinirSenha, senha efetivamente trocada).
// EN: Integration tests for the real ASP.NET Core Identity plumbing
//     (UserManager/SignInManager via EntityUserStore, see Authentication/EntityUserStore.cs)
//     — account lockout after consecutive invalid attempts, and the full
//     password-reset flow (request at /Conta/EsqueciSenha, link followed through to
//     /Conta/RedefinirSenha, password actually changed).
public class IdentityTests : TestesIntegracaoBase
{
    public IdentityTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<string> CriarFuncionarioAsync(string senha)
    {
        var login = $"teste.{Guid.NewGuid():N}"[..20];
        await Factory.UsandoContextoAsync(async db =>
        {
            var hasher = new PasswordHasher<Funcionario>();
            var funcionario = new Funcionario
            {
                Nome = "Teste Identity",
                Cargo = CargoFuncionario.Recepcionista,
                Login = login,
                Email = $"{login}@teste.local",
                // PT: Sem isso o Identity lança "User security stamp cannot be null" ao
                //     validar (ver EntityUserStore/Program.cs) — o mesmo motivo pelo
                //     qual o seed de Program.cs também precisou passar a setar isso.
                // EN: Without this Identity throws "User security stamp cannot be
                //     null" while validating (see EntityUserStore/Program.cs) — the
                //     same reason Program.cs's seed also had to start setting it.
                SecurityStamp = Guid.NewGuid().ToString()
            };
            funcionario.SenhaHash = hasher.HashPassword(funcionario, senha);
            db.Funcionarios.Add(funcionario);
            await db.SaveChangesAsync();
        });
        return login;
    }

    [Fact]
    public async Task LoginErradoVariasVezesSeguidas_BloqueiaAConta()
    {
        var login = await CriarFuncionarioAsync("SenhaCerta123");
        var client = CriarCliente();

        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 5; i++)
        {
            ultima = await PostComAntiforgeryAsync(client, "/Home/Login", "/Home/Login",
                new Dictionary<string, string> { ["login"] = login, ["senha"] = "errada" });
        }

        var corpo = await ultima!.Content.ReadAsStringAsync();
        Assert.Contains("bloqueada", corpo);

        // PT: Mesmo com a senha certa, o login continua recusado enquanto bloqueado.
        // EN: Even with the right password, login is still refused while locked out.
        var comSenhaCerta = await PostComAntiforgeryAsync(client, "/Home/Login", "/Home/Login",
            new Dictionary<string, string> { ["login"] = login, ["senha"] = "SenhaCerta123" });
        Assert.Equal(HttpStatusCode.OK, comSenhaCerta.StatusCode);
        Assert.Contains("bloqueada", await comSenhaCerta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RedefinirSenha_FluxoCompleto_TrocaASenhaDeVerdade()
    {
        var login = await CriarFuncionarioAsync("SenhaAntiga123");
        var client = CriarCliente();

        await PostComAntiforgeryAsync(client, "/Conta/EsqueciSenha", "/Conta/EsqueciSenha",
            new Dictionary<string, string> { ["email"] = $"{login}@teste.local" });

        // PT: O link não aparece mais na resposta HTTP (ver ContaController) — vai
        //     por e-mail de verdade, capturado aqui pela FakeEmailSender registrada
        //     em CustomWebApplicationFactory no lugar da SmtpEmailSender real.
        // EN: The link no longer appears in the HTTP response (see ContaController)
        //     — it goes out by real email, captured here through the FakeEmailSender
        //     CustomWebApplicationFactory registers in place of the real SmtpEmailSender.
        var email = Factory.EmailSender.Enviados.SingleOrDefault(e => e.Destinatario == $"{login}@teste.local");
        Assert.NotNull(email);

        var linkMatch = Regex.Match(email!.CorpoHtml, "href=\"([^\"]*RedefinirSenha[^\"]*)\"");
        Assert.True(linkMatch.Success, "Link de redefinição não encontrado no corpo do e-mail.");
        var link = WebUtility.HtmlDecode(linkMatch.Groups[1].Value);

        var telaRedefinir = await client.GetAsync(link);
        var corpoRedefinir = await telaRedefinir.Content.ReadAsStringAsync();

        string CampoOculto(string nome) =>
            WebUtility.HtmlDecode(Regex.Match(corpoRedefinir, $"name=\"{nome}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

        var campos = new Dictionary<string, string>
        {
            ["tipo"] = CampoOculto("tipo"),
            ["id"] = CampoOculto("id"),
            ["token"] = CampoOculto("token"),
            ["novaSenha"] = "SenhaNova456",
            ["__RequestVerificationToken"] = CampoOculto("__RequestVerificationToken"),
        };

        var resposta = await client.PostAsync("/Conta/RedefinirSenha", new FormUrlEncodedContent(campos));
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        var loginComSenhaAntiga = await PostComAntiforgeryAsync(client, "/Home/Login", "/Home/Login",
            new Dictionary<string, string> { ["login"] = login, ["senha"] = "SenhaAntiga123" });
        Assert.Equal(HttpStatusCode.OK, loginComSenhaAntiga.StatusCode);

        var loginComSenhaNova = await PostComAntiforgeryAsync(client, "/Home/Login", "/Home/Login",
            new Dictionary<string, string> { ["login"] = login, ["senha"] = "SenhaNova456" });
        Assert.Equal(HttpStatusCode.Redirect, loginComSenhaNova.StatusCode);
    }

    // PT: O caminho feliz acima já existia. Os testes abaixo cobrem o lado hostil, que
    //     estava sem nenhuma cobertura: reusar o token, usar o token de outra conta e
    //     adulterá-lo. São as três coisas que alguém tentaria com um link de redefinição
    //     que vazou — e nenhuma delas é evidente pela leitura do controller, porque quem
    //     garante a recusa é o Identity, não código deste projeto. Sem teste, uma troca
    //     futura do provedor de token poderia enfraquecer isso sem ninguém notar.
    // EN: The happy path above already existed. The tests below cover the hostile side,
    //     which had no coverage at all: reusing the token, using another account's token,
    //     and tampering with it. These are the three things someone would try with a leaked
    //     reset link — and none of them is evident from reading the controller, because what
    //     guarantees the refusal is Identity, not this project's code. Without tests, a
    //     future change of token provider could weaken this unnoticed.

    private async Task<(string tipo, string id, string token)> SolicitarRedefinicaoAsync(HttpClient client, string login)
    {
        await PostComAntiforgeryAsync(client, "/Conta/EsqueciSenha", "/Conta/EsqueciSenha",
            new Dictionary<string, string> { ["email"] = $"{login}@teste.local" });

        var email = Factory.EmailSender.Enviados.Last(e => e.Destinatario == $"{login}@teste.local");
        var link = WebUtility.HtmlDecode(
            Regex.Match(email.CorpoHtml, "href=\"([^\"]*RedefinirSenha[^\"]*)\"").Groups[1].Value);

        var corpo = await (await client.GetAsync(link)).Content.ReadAsStringAsync();
        string Campo(string nome) =>
            WebUtility.HtmlDecode(Regex.Match(corpo, $"name=\"{nome}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

        return (Campo("tipo"), Campo("id"), Campo("token"));
    }

    private async Task<HttpResponseMessage> RedefinirAsync(
        HttpClient client, string tipo, string id, string token, string novaSenha)
    {
        // PT: O token de antiforgery precisa vir de uma página que o emita; a de redefinição
        //     serve, e é a mesma que o usuário real teria aberto.
        // EN: The antiforgery token has to come from a page that issues one; the reset screen
        //     does, and it's the same one a real user would have opened.
        var pagina = await client.GetAsync($"/Conta/RedefinirSenha?tipo={tipo}&id={id}&token={Uri.EscapeDataString(token)}");
        var corpo = await pagina.Content.ReadAsStringAsync();
        var antiforgery = Regex.Match(corpo, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;

        return await client.PostAsync("/Conta/RedefinirSenha", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["tipo"] = tipo,
                ["id"] = id,
                ["token"] = token,
                ["novaSenha"] = novaSenha,
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(antiforgery)
            }));
    }

    private async Task<bool> ConsegueEntrarAsync(string login, string senha)
    {
        var client = CriarCliente();
        var resposta = await PostComAntiforgeryAsync(client, "/Home/Login", "/Home/Login",
            new Dictionary<string, string> { ["login"] = login, ["senha"] = senha });
        return resposta.StatusCode == HttpStatusCode.Redirect;
    }

    [Fact]
    public async Task TokenDeRedefinicao_NaoPodeSerUsadoDuasVezes()
    {
        // PT: Quem redefine invalida o próprio token, porque ResetPasswordAsync troca o
        //     SecurityStamp e o token carrega o stamp anterior. É o que impede um link
        //     vazado de continuar valendo depois de já ter sido usado.
        // EN: Resetting invalidates the token itself, because ResetPasswordAsync rotates the
        //     SecurityStamp and the token carries the previous one. That's what stops a leaked
        //     link from still working after it has already been used.
        var login = await CriarFuncionarioAsync("SenhaOriginal1");
        var client = CriarCliente();
        var (tipo, id, token) = await SolicitarRedefinicaoAsync(client, login);

        var primeira = await RedefinirAsync(client, tipo, id, token, "PrimeiraTroca1");
        Assert.Equal(HttpStatusCode.Redirect, primeira.StatusCode);

        var segunda = await RedefinirAsync(client, tipo, id, token, "SegundaTroca2");
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);

        Assert.True(await ConsegueEntrarAsync(login, "PrimeiraTroca1"));
        Assert.False(await ConsegueEntrarAsync(login, "SegundaTroca2"));
    }

    [Fact]
    public async Task TokenDeUmaConta_NaoRedefineOutra()
    {
        // PT: O token embute a identidade de quem o pediu. Trocar só o id na URL não
        //     transfere o poder do token para outra conta — que seria o ataque óbvio.
        // EN: The token embeds the identity of whoever requested it. Swapping just the id in
        //     the URL doesn't transfer the token's power to another account — which would be
        //     the obvious attack.
        var vitima = await CriarFuncionarioAsync("SenhaDaVitima1");
        var atacante = await CriarFuncionarioAsync("SenhaDoAtacante1");

        var client = CriarCliente();
        var (tipo, _, tokenDoAtacante) = await SolicitarRedefinicaoAsync(client, atacante);

        var idDaVitima = 0;
        await Factory.UsandoContextoAsync(async db =>
            idDaVitima = (await db.Funcionarios.FirstAsync(f => f.Login == vitima)).FuncionarioId);

        var resposta = await RedefinirAsync(client, tipo, idDaVitima.ToString(), tokenDoAtacante, "Invadida123");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False(await ConsegueEntrarAsync(vitima, "Invadida123"));
        Assert.True(await ConsegueEntrarAsync(vitima, "SenhaDaVitima1"));
    }

    [Fact]
    public async Task TokenAdulterado_NaoRedefine()
    {
        var login = await CriarFuncionarioAsync("SenhaIntacta1");
        var client = CriarCliente();
        var (tipo, id, token) = await SolicitarRedefinicaoAsync(client, login);

        // PT: Troca um caractere no meio do token. O provedor do Identity assina o valor, então
        //     qualquer alteração quebra a verificação.
        // EN: Flips one character in the middle of the token. Identity's provider signs the
        //     value, so any alteration breaks verification.
        var meio = token.Length / 2;
        var adulterado = token[..meio] + (token[meio] == 'A' ? 'B' : 'A') + token[(meio + 1)..];

        var resposta = await RedefinirAsync(client, tipo, id, adulterado, "Adulterada123");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False(await ConsegueEntrarAsync(login, "Adulterada123"));
        Assert.True(await ConsegueEntrarAsync(login, "SenhaIntacta1"));
    }
}
