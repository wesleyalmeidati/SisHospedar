using System.Net;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Models;

namespace SisHospedar.Tests;

// PT: Quatro bugs encontrados varrendo o que não tinha teste. O que os une é o modo de
//     falha: nenhum derruba a aplicação, nenhum aparece no log, e três deles a pessoa nem
//     percebe que aconteceu.
// EN: Four bugs found by sweeping what had no test. What unites them is the failure mode:
//     none crashes the application, none shows up in the log, and in three of them the
//     person doesn't even notice anything happened.
public class ValidacaoDeTelaTests : TestesIntegracaoBase
{
    public ValidacaoDeTelaTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<HttpClient> GestorLogadoAsync()
    {
        var client = CriarCliente();
        await LoginComoAsync(client, "luan.santos", "Senha123");
        return client;
    }

    // ---------- Tela de funcionário: erros do Identity eram invisíveis ----------

    [Fact]
    public async Task CriarFuncionarioComSenhaFraca_MostraOMotivo()
    {
        // PT: Antes: status 200, formulário recarregado, NENHUMA mensagem, funcionário não
        //     criado. A recepção clicava em salvar e não tinha como saber o que houve.
        //     A causa era a View: asp-validation-summary="ModelOnly" só exibe erro sem campo
        //     associado, e o controller joga os erros do Identity na chave "senha".
        // EN: Before: status 200, form reloaded, NO message, employee not created. Reception
        //     clicked save with no way to know what happened. The cause was the View:
        //     asp-validation-summary="ModelOnly" only shows errors with no associated field,
        //     and the controller puts Identity's errors under the "senha" key.
        var client = await GestorLogadoAsync();

        var resposta = await PostComAntiforgeryAsync(client, "/Funcionario/Create", "/Funcionario/Create",
            new Dictionary<string, string>
            {
                ["Nome"] = "Funcionario Senha Fraca",
                ["Cargo"] = ((int)CargoFuncionario.Recepcionista).ToString(),
                ["senha"] = "ab"
            });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

        // PT: O texto exato vem do Identity e pode mudar de versão; o que este teste garante
        //     é que ALGUMA explicação chega à tela, em vez de silêncio.
        // EN: The exact wording comes from Identity and can change between versions; what
        //     this test guarantees is that SOME explanation reaches the screen, not silence.
        Assert.Contains("validation-summary-errors", corpo);

        await Factory.UsandoContextoAsync(async db =>
            Assert.False(await db.Funcionarios.AnyAsync(f => f.Nome == "Funcionario Senha Fraca")));
    }

    [Fact]
    public async Task CriarFuncionarioComApostrofoNoNome_GeraLoginValido()
    {
        // PT: "Maria D'Angelo" gerava o login "maria.d'angelo". O apóstrofo não está em
        //     AllowedUserNameCharacters do Identity, o CreateAsync falhava, e — com o bug da
        //     View acima — a tela não dizia nada. Sobrenome com apóstrofo (D'Angelo, D'Ávila,
        //     O'Brien) simplesmente não cadastrava.
        // EN: "Maria D'Angelo" produced the login "maria.d'angelo". The apostrophe isn't in
        //     Identity's AllowedUserNameCharacters, CreateAsync failed, and — with the View
        //     bug above — the screen said nothing. A surname with an apostrophe (D'Angelo,
        //     D'Ávila, O'Brien) simply didn't register.
        var client = await GestorLogadoAsync();
        var nome = $"Maria D'Angelo {Guid.NewGuid():N}"[..24];

        var resposta = await PostComAntiforgeryAsync(client, "/Funcionario/Create", "/Funcionario/Create",
            new Dictionary<string, string>
            {
                ["Nome"] = nome,
                ["Cargo"] = ((int)CargoFuncionario.Recepcionista).ToString(),
                ["senha"] = "Senha123"
            });

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var criado = await db.Funcionarios.FirstOrDefaultAsync(f => f.Nome == nome);
            Assert.NotNull(criado);
            Assert.DoesNotContain('\'', criado!.Login);
            Assert.StartsWith("maria.", criado.Login);
        });
    }

    [Fact]
    public async Task CriarFuncionarioComAcento_MantemALetraBase()
    {
        // PT: Acento é DOBRADO, não descartado: "João" precisa virar "joao", não "joo". A
        //     limpeza de caracteres inválidos foi acrescentada depois da remoção de acento, e
        //     este teste existe para garantir que uma não engoliu a outra.
        // EN: Accents are FOLDED, not dropped: "João" has to become "joao", not "joo". The
        //     invalid-character stripping was added after the accent removal, and this test
        //     exists to ensure one didn't swallow the other.
        var client = await GestorLogadoAsync();
        var nome = $"João Conceição {Guid.NewGuid():N}"[..24];

        var resposta = await PostComAntiforgeryAsync(client, "/Funcionario/Create", "/Funcionario/Create",
            new Dictionary<string, string>
            {
                ["Nome"] = nome,
                ["Cargo"] = ((int)CargoFuncionario.Recepcionista).ToString(),
                ["senha"] = "Senha123"
            });

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var criado = await db.Funcionarios.FirstOrDefaultAsync(f => f.Nome == nome);
            Assert.NotNull(criado);
            Assert.StartsWith("joao.concei", criado!.Login);
        });
    }

    // ---------- Ficha anonimizada não pode ser reeditada ----------

    private async Task<int> CriarHospedeAnonimizadoAsync()
    {
        var id = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = new Hospede
            {
                Nome = "Titular removido",
                Cpf = "TEMP",
                Telefone = string.Empty,
                DataNascimento = new DateTime(1900, 1, 1)
            };
            db.Hospedes.Add(hospede);
            await db.SaveChangesAsync();

            hospede.Cpf = $"{Hospede.PrefixoCpfAnonimizado}{hospede.HospedeId:D8}";
            await db.SaveChangesAsync();
            id = hospede.HospedeId;
        });
        return id;
    }

    [Fact]
    public async Task AbrirEdicaoDeFichaAnonimizada_ERecusadoComMensagem()
    {
        // PT: Antes o GET abria normalmente e só o POST falhava — com "CPF inválido" num
        //     campo que a recepção não tinha tocado, porque o marcador de anonimização não
        //     passa no CpfValido. Não havia saída: a ficha era ineditável e a mensagem
        //     apontava para o lugar errado.
        // EN: Before, the GET opened normally and only the POST failed — with "CPF inválido"
        //     on a field reception hadn't touched, because the anonymization marker doesn't
        //     pass CpfValido. There was no way out: the record was uneditable and the message
        //     pointed at the wrong place.
        var id = await CriarHospedeAnonimizadoAsync();
        var client = await GestorLogadoAsync();

        var resposta = await client.GetAsync($"/Hospede/Edit/{id}");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
    }

    [Fact]
    public async Task SalvarEdicaoDeFichaAnonimizada_NaoRepersonalizaOsDados()
    {
        // PT: A checagem do POST é feita contra a linha GRAVADA, não contra o formulário —
        //     senão bastaria enviar um CPF válido qualquer no corpo para "desanonimizar" a
        //     ficha, que é justamente o que a anonimização existe para impedir.
        // EN: The POST check runs against the STORED row, not the form — otherwise posting
        //     any valid CPF in the body would be enough to "de-anonymize" the record, which
        //     is exactly what anonymization exists to prevent.
        var id = await CriarHospedeAnonimizadoAsync();
        var client = await GestorLogadoAsync();

        var resposta = await PostComAntiforgeryAsync(client, "/Hospede", $"/Hospede/Edit/{id}",
            new Dictionary<string, string>
            {
                ["HospedeId"] = id.ToString(),
                ["Nome"] = "Nome Reinserido",
                ["Cpf"] = "529.982.247-25",
                ["Telefone"] = "(41) 99999-0000",
                ["DataNascimento"] = "1990-01-01"
            });

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = await db.Hospedes.FirstAsync(h => h.HospedeId == id);
            Assert.Equal("Titular removido", hospede.Nome);
            Assert.True(hospede.EstaAnonimizado);
        });
    }

    // ---------- Prorrogação de estadia ----------

    private async Task<(HttpClient client, int hospedagemId)> HospedeComEstadiaAtivaAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var client = CriarCliente();

        await PostComAntiforgeryAsync(client, "/Cliente/Registrar", "/Cliente/Registrar",
            new Dictionary<string, string>
            {
                ["Nome"] = "Hospede Prorroga",
                ["Cpf"] = GerarCpfValido(),
                ["Telefone"] = "(41) 90000-0000",
                ["DataNascimento"] = "1990-01-01",
                ["Email"] = $"p{sufixo}@exemplo.com",
                ["Login"] = $"p{sufixo}",
                ["senha"] = "Senha123"
            });

        var hospedagemId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = await db.Hospedes.FirstAsync(h => h.Login == $"p{sufixo}");
            var apartamento = new Apartamento
            {
                Numero = $"X{Guid.NewGuid():N}"[..9],
                Capacidade = 2, ValorDiaria = 150m, Status = StatusApartamento.Ocupado
            };
            db.Apartamentos.Add(apartamento);
            await db.SaveChangesAsync();

            var hospedagem = new Hospedagem
            {
                HospedeId = hospede.HospedeId,
                ApartamentoId = apartamento.ApartamentoId,
                DataEntrada = DateTime.Today.AddDays(-3),
                DataSaida = DateTime.Today.AddDays(2)
            };
            db.Hospedagens.Add(hospedagem);
            await db.SaveChangesAsync();
            hospedagemId = hospedagem.HospedagemId;
        });

        return (client, hospedagemId);
    }

    private static string GerarCpfValido()
    {
        var digitos = new int[11];
        for (var i = 0; i < 9; i++) digitos[i] = Random.Shared.Next(0, 9);
        for (var posicao = 9; posicao < 11; posicao++)
        {
            var soma = 0;
            for (var i = 0; i < posicao; i++) soma += digitos[i] * (posicao + 1 - i);
            var resto = soma % 11;
            digitos[posicao] = resto < 2 ? 0 : 11 - resto;
        }
        return string.Concat(digitos);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    public async Task Prorrogar_ParaDataQueNaoEFutura_NaoAltera(int diasRelativosAHoje)
    {
        // PT: A única checagem era "depois da data de entrada". Numa estadia iniciada há três
        //     dias, isso deixava prorrogar para ANTEONTEM: a hospedagem ficava com saída
        //     anterior a hoje e ainda aberta, com o apartamento Ocupado. A ação chama-se
        //     prorrogar; a data tem que ser futura.
        // EN: The only check was "after the check-in date". On a stay that started three days
        //     ago, that allowed extending to THE DAY BEFORE YESTERDAY: the stay ended up with
        //     a checkout before today and still open, with the room Ocupado. The action is
        //     called "extend"; the date has to be in the future.
        var (client, hospedagemId) = await HospedeComEstadiaAtivaAsync();
        var original = DateTime.Today.AddDays(2);

        await PostComAntiforgeryAsync(client, "/Cliente/MinhaConta", "/Cliente/Prorrogar",
            new Dictionary<string, string>
            {
                ["hospedagemId"] = hospedagemId.ToString(),
                ["novaDataSaida"] = DateTime.Today.AddDays(diasRelativosAHoje).ToString("yyyy-MM-dd")
            });

        await Factory.UsandoContextoAsync(async db =>
        {
            var hospedagem = await db.Hospedagens.FirstAsync(h => h.HospedagemId == hospedagemId);
            Assert.Equal(original, hospedagem.DataSaida);
        });
    }

    [Fact]
    public async Task Prorrogar_ParaAnoAbsurdo_NaoAltera()
    {
        var (client, hospedagemId) = await HospedeComEstadiaAtivaAsync();
        var original = DateTime.Today.AddDays(2);

        await PostComAntiforgeryAsync(client, "/Cliente/MinhaConta", "/Cliente/Prorrogar",
            new Dictionary<string, string>
            {
                ["hospedagemId"] = hospedagemId.ToString(),
                ["novaDataSaida"] = "2060-01-01"
            });

        await Factory.UsandoContextoAsync(async db =>
        {
            var hospedagem = await db.Hospedagens.FirstAsync(h => h.HospedagemId == hospedagemId);
            Assert.Equal(original, hospedagem.DataSaida);
        });
    }

    [Fact]
    public async Task Prorrogar_ParaDataFuturaRazoavel_Funciona()
    {
        // PT: O contrapeso. Sem ele, "nunca prorroga" passaria nos três acima.
        // EN: The counterweight. Without it, "never extends" would pass all three above.
        var (client, hospedagemId) = await HospedeComEstadiaAtivaAsync();
        var nova = DateTime.Today.AddDays(10);

        await PostComAntiforgeryAsync(client, "/Cliente/MinhaConta", "/Cliente/Prorrogar",
            new Dictionary<string, string>
            {
                ["hospedagemId"] = hospedagemId.ToString(),
                ["novaDataSaida"] = nova.ToString("yyyy-MM-dd")
            });

        await Factory.UsandoContextoAsync(async db =>
        {
            var hospedagem = await db.Hospedagens.FirstAsync(h => h.HospedagemId == hospedagemId);
            Assert.Equal(nova, hospedagem.DataSaida);
        });
    }
}
