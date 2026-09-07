using System.Net;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Models;

namespace SisHospedar.Tests;

// PT: Testes de integração para as transições de Apartamento.Status feitas por
//     HospedagemController — check-in (Create) ocupa o quarto, check-out (Finalizar) libera.
// EN: Integration tests for the Apartamento.Status transitions driven by
//     HospedagemController — check-in (Create) occupies the room, check-out (Finalizar) frees it.
public class HospedagemStatusTests : TestesIntegracaoBase
{
    public HospedagemStatusTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<(int hospedeId, int apartamentoId)> CriarHospedeEApartamentoAsync(StatusApartamento status)
    {
        int hospedeId = 0, apartamentoId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = new Hospede
            {
                Nome = "Teste Hospedagem",
                Cpf = Guid.NewGuid().ToString("N")[..14],
                DataNascimento = new DateTime(1990, 1, 1)
            };
            var apartamento = new Apartamento
            {
                Numero = $"H{Guid.NewGuid():N}"[..9],
                Capacidade = 2,
                ValorDiaria = 100m,
                Status = status
            };
            db.Hospedes.Add(hospede);
            db.Apartamentos.Add(apartamento);
            await db.SaveChangesAsync();
            hospedeId = hospede.HospedeId;
            apartamentoId = apartamento.ApartamentoId;
        });
        return (hospedeId, apartamentoId);
    }

    [Fact]
    public async Task Create_OcupaApartamentoDisponivel()
    {
        var (hospedeId, apartamentoId) = await CriarHospedeEApartamentoAsync(StatusApartamento.Disponivel);
        var client = CriarCliente();
        await LoginComoAsync(client, "matias.sampaio", "Senha123");

        var resposta = await PostComAntiforgeryAsync(client, "/Hospedagem/Create", "/Hospedagem/Create",
            new Dictionary<string, string>
            {
                ["HospedeId"] = hospedeId.ToString(),
                ["ApartamentoId"] = apartamentoId.ToString(),
                ["DataEntrada"] = DateTime.Today.ToString("yyyy-MM-dd")
            });

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var apartamento = await db.Apartamentos.FindAsync(apartamentoId);
            Assert.Equal(StatusApartamento.Ocupado, apartamento!.Status);
            Assert.True(await db.Hospedagens.AnyAsync(h => h.ApartamentoId == apartamentoId && !h.Finalizada));
        });
    }

    [Theory]
    [InlineData(StatusApartamento.Ocupado)]
    [InlineData(StatusApartamento.Manutencao)]
    public async Task Create_RejeitaApartamentoIndisponivel(StatusApartamento status)
    {
        var (hospedeId, apartamentoId) = await CriarHospedeEApartamentoAsync(status);
        var client = CriarCliente();
        await LoginComoAsync(client, "matias.sampaio", "Senha123");

        var resposta = await PostComAntiforgeryAsync(client, "/Hospedagem/Create", "/Hospedagem/Create",
            new Dictionary<string, string>
            {
                ["HospedeId"] = hospedeId.ToString(),
                ["ApartamentoId"] = apartamentoId.ToString(),
                ["DataEntrada"] = DateTime.Today.ToString("yyyy-MM-dd")
            });

        // PT: Falha de validação re-renderiza o form (200), não redireciona.
        // EN: Validation failure re-renders the form (200), it doesn't redirect.
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var apartamento = await db.Apartamentos.FindAsync(apartamentoId);
            Assert.Equal(status, apartamento!.Status);
            Assert.False(await db.Hospedagens.AnyAsync(h => h.ApartamentoId == apartamentoId));
        });
    }

    [Fact]
    public async Task FinalizarConfirmado_LiberaApartamento()
    {
        var (hospedeId, apartamentoId) = await CriarHospedeEApartamentoAsync(StatusApartamento.Ocupado);
        int hospedagemId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospedagem = new Hospedagem
            {
                HospedeId = hospedeId,
                ApartamentoId = apartamentoId,
                DataEntrada = DateTime.Today.AddDays(-2)
            };
            db.Hospedagens.Add(hospedagem);
            await db.SaveChangesAsync();
            hospedagemId = hospedagem.HospedagemId;
        });

        var client = CriarCliente();
        await LoginComoAsync(client, "matias.sampaio", "Senha123");

        // PT: Reproduz o form real de Views/Hospedagem/Finalizar.cshtml, que posta para
        //     /Hospedagem/Finalizar/{id} via asp-route-id (o id vai na URL, não no corpo).
        // EN: Reproduces the real form in Views/Hospedagem/Finalizar.cshtml, which posts
        //     to /Hospedagem/Finalizar/{id} via asp-route-id (id in the URL, not the body).
        var resposta = await PostComAntiforgeryAsync(client,
            $"/Hospedagem/Finalizar/{hospedagemId}", $"/Hospedagem/Finalizar/{hospedagemId}",
            new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var hospedagem = await db.Hospedagens.FindAsync(hospedagemId);
            Assert.True(hospedagem!.Finalizada);
            Assert.Equal(DateTime.Today, hospedagem.DataSaida);

            var apartamento = await db.Apartamentos.FindAsync(apartamentoId);
            Assert.Equal(StatusApartamento.Disponivel, apartamento!.Status);
        });
    }
}
