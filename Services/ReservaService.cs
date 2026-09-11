using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Services
{
    public class ReservaService : IReservaService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ReservaService> _logger;

        public ReservaService(AppDbContext context, ILogger<ReservaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Sucesso, string? Erro)> CriarPedidoAsync(Reserva reserva)
        {
            if (reserva.DataSaidaPrevista <= reserva.DataEntrada)
                return (false, "A data de saída deve ser depois da data de entrada.");

            if (reserva.DataEntrada < DateTime.Today)
                return (false, "A data de entrada não pode ser no passado.");

            var apartamento = await _context.Apartamentos.FindAsync(reserva.ApartamentoId);
            if (apartamento == null || apartamento.Status != StatusApartamento.Disponivel)
                return (false, "Apartamento não está mais disponível.");

            // PT: Apartamento.Status sozinho não evita reserva duplicada — ele só vira
            //     Ocupado quando UMA das reservas é confirmada (check-in), então duas
            //     pessoas podem pedir o mesmo quarto no mesmo período enquanto ambas
            //     ainda estão Pendente. Aqui barramos isso checando sobreposição de
            //     datas contra qualquer reserva já Pendente ou Confirmada no apartamento.
            // EN: Apartamento.Status alone doesn't prevent a duplicate booking — it only
            //     becomes Ocupado when ONE of the requests is confirmed (check-in), so
            //     two people can request the same room for the same period while both
            //     are still Pendente. This blocks that by checking date overlap against
            //     any Pendente or Confirmada reserva already on the apartment.
            var conflito = await _context.Reservas.AnyAsync(r =>
                r.ApartamentoId == reserva.ApartamentoId &&
                (r.Status == StatusReserva.Pendente || r.Status == StatusReserva.Confirmada) &&
                r.DataEntrada < reserva.DataSaidaPrevista &&
                r.DataSaidaPrevista > reserva.DataEntrada);
            if (conflito)
            {
                _logger.LogWarning(
                    "Pedido de reserva recusado por conflito de datas: apartamento {ApartamentoId}, período {Entrada:yyyy-MM-dd} a {Saida:yyyy-MM-dd}.",
                    reserva.ApartamentoId, reserva.DataEntrada, reserva.DataSaidaPrevista);
                return (false, "Já existe uma reserva para este apartamento nesse período.");
            }

            // PT: Marca o apartamento como Reservado — ele some das listas de "disponível
            //     pra reservar/check-in" (ReservaController.Create, HospedagemController.Create)
            //     até a recepção confirmar (vira Ocupado) ou cancelar (volta a Disponivel).
            // EN: Marks the apartment as Reservado — it drops out of the "available to
            //     book/check-in" lists (ReservaController.Create, HospedagemController.Create)
            //     until reception confirms it (becomes Ocupado) or cancels it (goes back
            //     to Disponivel).
            apartamento.Status = StatusApartamento.Reservado;
            reserva.Status = StatusReserva.Pendente;
            _context.Add(reserva);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Pedido de reserva criado: reserva {ReservaId}, apartamento {ApartamentoId} marcado como Reservado.",
                reserva.ReservaId, reserva.ApartamentoId);
            return (true, null);
        }

        public async Task<string?> ConfirmarAsync(int reservaId)
        {
            var reserva = await _context.Reservas
                .Include(r => r.Apartamento)
                .FirstOrDefaultAsync(r => r.ReservaId == reservaId);

            if (reserva == null || reserva.Status != StatusReserva.Pendente)
                return null;

            if (reserva.Apartamento == null || reserva.Apartamento.Status != StatusApartamento.Reservado)
            {
                _logger.LogWarning(
                    "Confirmação recusada: reserva {ReservaId} aponta para apartamento {ApartamentoId} em {Status}, esperado Reservado.",
                    reservaId, reserva.ApartamentoId, reserva.Apartamento?.Status.ToString() ?? "inexistente");
                return "Apartamento não está mais disponível para confirmar esta reserva.";
            }

            // PT: Confirmar a reserva = fazer o check-in (cria a Hospedagem de verdade).
            // EN: Confirming the booking = performing check-in (creates the actual Hospedagem).
            var hospedagem = new Hospedagem
            {
                HospedeId = reserva.HospedeId,
                ApartamentoId = reserva.ApartamentoId,
                DataEntrada = reserva.DataEntrada,
                DataSaida = reserva.DataSaidaPrevista
            };

            reserva.Apartamento.Status = StatusApartamento.Ocupado;
            reserva.Status = StatusReserva.Confirmada;

            _context.Add(hospedagem);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Reserva {ReservaId} confirmada: hospedagem {HospedagemId} criada, apartamento {ApartamentoId} ocupado.",
                reservaId, hospedagem.HospedagemId, reserva.ApartamentoId);
            return "Reserva confirmada e check-in realizado.";
        }

        public async Task<bool> CancelarAsync(int reservaId)
        {
            var reserva = await _context.Reservas
                .Include(r => r.Apartamento)
                .FirstOrDefaultAsync(r => r.ReservaId == reservaId);
            if (reserva == null || reserva.Status != StatusReserva.Pendente) return false;

            reserva.Status = StatusReserva.Cancelada;
            if (reserva.Apartamento != null && reserva.Apartamento.Status == StatusApartamento.Reservado)
                reserva.Apartamento.Status = StatusApartamento.Disponivel;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Reserva {ReservaId} cancelada; apartamento {ApartamentoId} liberado.",
                reservaId, reserva.ApartamentoId);
            return true;
        }
    }
}
