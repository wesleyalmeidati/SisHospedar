using System.Net;

namespace SisHospedar.Tests;

// PT: Testes de integração do autocadastro de hóspede (ClienteController.Registrar) —
//     validação de CPF de verdade (CpfValidoAttribute, algoritmo módulo 11) e os
//     campos obrigatórios só desta tela (Login/Email — ver o comentário no controller
//     sobre por que eles não são [Required] direto no model Hospede). Os asserts sobre
//     mensagem de erro rodam WebUtility.HtmlDecode no corpo da resposta primeiro — o
//     HtmlEncoder padrão do ASP.NET Core escapa acentos como entidade numérica
//     ("inválido" vira "inv&#xE1;lido"), então comparar a string exibida ao usuário
//     (com acento) direto contra o HTML cru nunca bateria.
// EN: Integration tests for guest self-registration (ClienteController.Registrar) —
//     real CPF validation (CpfValidoAttribute, modulo-11 algorithm) and the fields
//     required only on this screen (Login/Email — see the controller comment on why
//     they aren't [Required] directly on the Hospede model). The error-message
//     asserts run WebUtility.HtmlDecode on the response body first — ASP.NET Core's
//     default HtmlEncoder escapes accented characters as numeric entities
//     ("inválido" becomes "inv&#xE1;lido"), so comparing the string as shown to the
//     user (with the accent) against the raw HTML would never match.
public class RegistroHospedeTests : TestesIntegracaoBase
{
    public RegistroHospedeTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static Dictionary<string, string> CamposValidos(string sufixo) => new()
    {
        ["Nome"] = $"Teste Registro {sufixo}",
        ["Cpf"] = "789.012.345-05",
        ["Email"] = $"registro.{sufixo}@teste.local",
        ["Telefone"] = "(41) 90000-0000",
        ["DataNascimento"] = "1990-01-01",
        ["Login"] = $"registro.{sufixo}",
        ["senha"] = "SenhaTeste123"
    };

    [Fact]
    public async Task Registrar_ComCpfInvalido_MostraErroDeValidacao()
    {
        var client = CriarCliente();
        var campos = CamposValidos("cpfinvalido");
        // PT: 11 dígitos, formato certo, mas o dígito verificador está errado —
        //     é exatamente o tipo de erro que só o algoritmo (não um regex de
        //     formato) consegue pegar.
        // EN: 11 digits, right shape, but the check digit is wrong — exactly the
        //     kind of mistake only the algorithm (not a format regex) can catch.
        campos["Cpf"] = "111.222.333-44";

        var resposta = await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar", campos);
        var corpo = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("CPF inválido", corpo);
    }

    [Fact]
    public async Task Registrar_ComCpfDeDigitosRepetidos_MostraErroDeValidacao()
    {
        var client = CriarCliente();
        var campos = CamposValidos("cpfrepetido");
        // PT: Satisfaz a fórmula do módulo 11 matematicamente, mas nunca é um CPF
        //     real emitido — ver comentário do CpfValidoAttribute.
        // EN: Mathematically satisfies the modulo-11 formula, but is never a real
        //     issued CPF — see the CpfValidoAttribute comment.
        campos["Cpf"] = "111.111.111-11";

        var resposta = await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar", campos);
        var corpo = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("CPF inválido", corpo);
    }

    [Fact]
    public async Task Registrar_SemLoginNemEmail_MostraErrosDeCampoObrigatorio()
    {
        var client = CriarCliente();
        var campos = CamposValidos("semloginemail");
        campos["Login"] = "";
        campos["Email"] = "";

        var resposta = await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar", campos);
        var corpo = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("O login é obrigatório.", corpo);
        Assert.Contains("O e-mail é obrigatório.", corpo);
    }

    [Fact]
    public async Task Registrar_ComDadosValidos_CriaContaComSucesso()
    {
        var client = CriarCliente();
        var campos = CamposValidos("sucesso");

        var resposta = await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar", campos);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
    }
}
