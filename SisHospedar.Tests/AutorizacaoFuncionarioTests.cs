using System.Net;
using SisHospedar.Models;

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

        // PT: O handler de cookie monta uma URL absoluta com ?ReturnUrl=... (nunca
        //     existia antes com Session) — checamos só o caminho, não a query string.
        // EN: The cookie handler builds an absolute URL with ?ReturnUrl=... (never
        //     existed before with Session) — we only check the path, not the query string.
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/Home/Login", resposta.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Recepcionista_AcessaHospedagem_MasNaoAcessaFuncionario()
    {
        var client = CriarCliente();
        await LoginComoAsync(client, "matias.sampaio", "Senha123");

        var hospedagem = await client.GetAsync("/Hospedagem");
        Assert.Equal(HttpStatusCode.OK, hospedagem.StatusCode);

        var funcionario = await client.GetAsync("/Funcionario");
        Assert.Equal(HttpStatusCode.Forbidden, funcionario.StatusCode);
    }

    [Theory]
    [InlineData("wesley.sales")]
    [InlineData("luan.santos")]
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
            new Dictionary<string, string> { ["login"] = "wesley.sales", ["senha"] = "senha-errada" });

        // PT: Login inválido apenas re-renderiza a view (200), não redireciona.
        // EN: Invalid login just re-renders the view (200), it doesn't redirect.
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var painel = await client.GetAsync("/Painel");
        Assert.Equal(HttpStatusCode.Redirect, painel.StatusCode);
    }

    [Fact]
    public async Task Funcionario_VeOFormularioDeReservaMasNaoCriaComoHospede()
    {
        // PT: A tela de reserva passou a ser pública, para o visitante escolher antes de
        //     criar conta — então um funcionário também a enxerga, e isso não é problema:
        //     é um formulário, não um dado de outra identidade.
        //     A separação entre as identidades continua valendo onde importa: no POST, que
        //     é a ação que cria a reserva. O cookie de funcionário é de outro esquema, então
        //     AuthenticateAsync(Esquemas.Hospede) falha e ele é mandado ao login de hóspede
        //     em vez de a reserva ser criada em nome de alguém.
        // EN: The booking screen became public so a visitor can choose before creating an
        //     account — so an employee sees it too, and that's fine: it's a form, not
        //     another identity's data.
        //     Identity separation still holds where it matters: on the POST, the action that
        //     creates the booking. The employee cookie belongs to a different scheme, so
        //     AuthenticateAsync(Esquemas.Hospede) fails and they're sent to the guest login
        //     instead of a booking being created in someone's name.
        var apartamentoId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var apartamento = new Apartamento
            {
                Numero = $"F{Guid.NewGuid():N}"[..9],
                Capacidade = 2,
                ValorDiaria = 150m,
                Status = StatusApartamento.Disponivel
            };
            db.Apartamentos.Add(apartamento);
            await db.SaveChangesAsync();
            apartamentoId = apartamento.ApartamentoId;
        });

        var client = CriarCliente();
        await LoginComoAsync(client, "matias.sampaio", "Senha123");

        var pagina = await client.GetAsync("/Reserva/Create");
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);

        var resposta = await PostComAntiforgeryAsync(client, "/Reserva/Create", "/Reserva/Create",
            new Dictionary<string, string>
            {
                ["ApartamentoId"] = apartamentoId.ToString(),
                ["DataEntrada"] = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd"),
                ["DataSaidaPrevista"] = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd")
            });

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        // PT: Location vem relativo aqui porque o redirect parte do próprio controller, não
        //     do handler de cookie — Uri.AbsolutePath lançaria numa URI relativa.
        // EN: Location is relative here because the redirect comes from the controller
        //     itself, not the cookie handler — Uri.AbsolutePath would throw on a relative URI.
        Assert.StartsWith("/Cliente/Login", resposta.Headers.Location!.ToString());
    }
}
