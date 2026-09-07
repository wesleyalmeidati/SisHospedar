using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
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
}
