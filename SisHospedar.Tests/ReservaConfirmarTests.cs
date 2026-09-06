using System.Net;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Models;

namespace SisHospedar.Tests;

// PT: Testes de integração para o ponto de check-in real do sistema:
//     ReservaController.Confirmar cria a Hospedagem e ocupa o apartamento.
// EN: Integration tests for the system's actual check-in point:
//     ReservaController.Confirmar creates the Hospedagem and occupies the room.
public class ReservaConfirmarTests : TestesIntegracaoBase
{
    public ReservaConfirmarTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<int> CriarReservaPendenteAsync(StatusApartamento statusApartamento)
    {
        int reservaId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = new Hospede
            {
                Nome = "Teste Reserva",
                Cpf = Guid.NewGuid().ToString("N")[..14],
                DataNascimento = new DateTime(1990, 1, 1)
            };
            var apartamento = new Apartamento
            {
                Numero = $"R{Guid.NewGuid():N}"[..9],
                Capacidade = 2,
                ValorDiaria = 150m,
                Status = statusApartamento
            };
            db.Hospedes.Add(hospede);
            db.Apartamentos.Add(apartamento);
            await db.SaveChangesAsync();

            var reserva = new Reserva
            {
                HospedeId = hospede.HospedeId,
                ApartamentoId = apartamento.ApartamentoId,
                DataEntrada = DateTime.Today,
                DataSaidaPrevista = DateTime.Today.AddDays(3),
                Status = StatusReserva.Pendente
            };
            db.Reservas.Add(reserva);
            await db.SaveChangesAsync();
            reservaId = reserva.ReservaId;
        });
        return reservaId;
    }

    [Fact]
    public async Task Confirmar_CriaHospedagemEOcupaApartamento()
    {
        var reservaId = await CriarReservaPendenteAsync(StatusApartamento.Disponivel);
        var client = CriarCliente();
        await LoginComoAsync(client, "recepcao", "Senha123");

        var resposta = await PostComAntiforgeryAsync(
            client, "/Reserva", $"/Reserva/Confirmar/{reservaId}", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var reserva = await db.Reservas.FindAsync(reservaId);
            Assert.Equal(StatusReserva.Confirmada, reserva!.Status);

            var apartamento = await db.Apartamentos.FindAsync(reserva.ApartamentoId);
            Assert.Equal(StatusApartamento.Ocupado, apartamento!.Status);

            Assert.True(await db.Hospedagens.AnyAsync(h =>
                h.HospedeId == reserva.HospedeId && h.ApartamentoId == reserva.ApartamentoId && !h.Finalizada));
        });
    }

    [Fact]
    public async Task Confirmar_FalhaGraciosamenteSeApartamentoIndisponivel()
    {
        var reservaId = await CriarReservaPendenteAsync(StatusApartamento.Ocupado);
        var client = CriarCliente();
        await LoginComoAsync(client, "recepcao", "Senha123");

        var resposta = await PostComAntiforgeryAsync(
            client, "/Reserva", $"/Reserva/Confirmar/{reservaId}", new Dictionary<string, string>());

        // PT: Não é um erro HTTP — a action redireciona de volta para Index com um aviso.
        // EN: Not an HTTP error — the action redirects back to Index with a warning.
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var reserva = await db.Reservas.FindAsync(reservaId);
            Assert.Equal(StatusReserva.Pendente, reserva!.Status);
            Assert.False(await db.Hospedagens.AnyAsync(h => h.HospedeId == reserva.HospedeId));
        });
    }
}
