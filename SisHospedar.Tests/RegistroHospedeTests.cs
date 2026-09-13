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

    // PT: O CPF é gerado por chamada, não fixo. Com um CPF fixo os testes ficam acoplados
    //     por estado compartilhado: enquanto todos os casos negativos são recusados antes
    //     do insert, nada colide e o problema fica invisível — mas basta uma validação
    //     deixar de recusar para dois testes criarem a mesma linha e um terceiro falhar por
    //     unicidade, longe da causa. Foi exatamente o que aconteceu ao reverter a validação
    //     de data para conferir se os testes novos pegavam a regressão.
    // EN: The CPF is generated per call, not fixed. With a fixed CPF the tests are coupled
    //     through shared state: as long as every negative case is refused before the insert,
    //     nothing collides and the problem stays invisible — but the moment one validation
    //     stops refusing, two tests create the same row and a third fails on uniqueness, far
    //     from the cause. That's exactly what happened when reverting the date validation to
    //     check whether the new tests caught the regression.
    private static Dictionary<string, string> CamposValidos(string sufixo) => new()
    {
        ["Nome"] = $"Teste Registro {sufixo}",
        ["Cpf"] = GerarCpfValido(),
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

    // PT: Os quatro testes abaixo nasceram de uma rodada de teste adversarial contra o
    //     formulário: nascimento em 2050, nascimento amanhã, nascimento em 1750 e
    //     "nao-e-telefone" eram todos ACEITOS. Nenhum quebrava nada visivelmente — só
    //     enchiam o cadastro de dado impossível, e o hotel ficava com ficha sem contato.
    // EN: The four tests below came out of an adversarial run against the form: a 2050
    //     date of birth, a tomorrow date of birth, a 1750 one and "nao-e-telefone" were
    //     all ACCEPTED. None broke anything visibly — they just filled the registry with
    //     impossible data, leaving the hotel with records it couldn't call.

    [Theory]
    [InlineData("2050-01-01")]
    [InlineData("2099-12-31")]
    public async Task Registrar_ComNascimentoNoFuturo_MostraErro(string data)
    {
        var client = CriarCliente();
        var campos = CamposValidos($"futuro{data.Replace("-", "")}");
        campos["DataNascimento"] = data;

        var resposta = await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar", campos);
        var corpo = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("não pode ser no futuro", corpo);
    }

    [Fact]
    public async Task Registrar_ComNascimentoAmanha_MostraErro()
    {
        // PT: O caso de borda que um "ano > ano atual" ingênuo deixaria passar.
        // EN: The edge case a naive "year > current year" check would let through.
        var client = CriarCliente();
        var campos = CamposValidos("amanha");
        campos["DataNascimento"] = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

        var resposta = await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar", campos);
        var corpo = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("não pode ser no futuro", corpo);
    }

    [Fact]
    public async Task Registrar_ComNascimentoImplausivelmenteAntigo_MostraErro()
    {
        var client = CriarCliente();
        var campos = CamposValidos("antigo");
        campos["DataNascimento"] = "1750-01-01";

        var resposta = await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar", campos);
        var corpo = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("parece incorreto", corpo);
    }

    [Fact]
    public async Task Registrar_ComTelefoneSemDigitos_MostraErro()
    {
        var client = CriarCliente();
        var campos = CamposValidos("semtelefone");
        campos["Telefone"] = "nao-e-telefone";

        var resposta = await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar", campos);
        var corpo = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("telefone válido", corpo);
    }

    // PT: Formato livre é aceito de propósito — a validação conta dígitos, não impõe
    //     máscara. Este teste existe para impedir que alguém "melhore" o validador com um
    //     regex de formato e passe a recusar fixo, internacional ou número sem parênteses.
    // EN: Free formatting is accepted on purpose — the validation counts digits, it doesn't
    //     impose a mask. This test exists to stop someone "improving" the validator with a
    //     format regex and start refusing landlines, international numbers or ones written
    //     without parentheses.
    [Theory]
    [InlineData("4130001234")]
    [InlineData("+55 41 99999-8888")]
    [InlineData("(41) 3000-1234")]
    public async Task Registrar_AceitaTelefoneEmFormatosDiferentes(string telefone)
    {
        var client = CriarCliente();
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var campos = CamposValidos(sufixo);
        campos["Telefone"] = telefone;

        var resposta = await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar", campos);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
    }

    // PT: CPF sintético com dígitos verificadores calculados — CpfValidoAttribute recusaria
    //     um número inventado.
    // EN: A synthetic CPF with calculated check digits — CpfValidoAttribute would reject a
    //     made-up number.
    private static string GerarCpfValido()
    {
        var aleatorio = Random.Shared;
        var digitos = new int[11];
        for (var i = 0; i < 9; i++) digitos[i] = aleatorio.Next(0, 9);

        for (var posicao = 9; posicao < 11; posicao++)
        {
            var soma = 0;
            for (var i = 0; i < posicao; i++) soma += digitos[i] * (posicao + 1 - i);
            var resto = soma % 11;
            digitos[posicao] = resto < 2 ? 0 : 11 - resto;
        }

        return string.Concat(digitos);
    }
}
