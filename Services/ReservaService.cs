using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Services
{
    public class ReservaService : IReservaService
    {
        private readonly AppDbContext _context;
        public ReservaService(AppDbContext context) => _context = context;

        public async Task<(bool Sucesso, string? Erro)> CriarPedidoAsync(Reserva reserva)
        {
            if (reserva.DataSaidaPrevista <= reserva.DataEntrada)
                return (false, "A data de saída deve ser depois da data de entrada.");

            if (reserva.DataEntrada < DateTime.Today)
                return (false, "A data de entrada não pode ser no passado.");

            var apartamento = await _context.Apartamentos.FindAsync(reserva.ApartamentoId);
            if (apartamento == null || apartamento.Status != StatusApartamento.Disponivel)
                return (false, "Apartamento não está mais disponível.");

            reserva.Status = StatusReserva.Pendente;
            _context.Add(reserva);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<string?> ConfirmarAsync(int reservaId)
        {
            var reserva = await _context.Reservas
                .Include(r => r.Apartamento)
                .FirstOrDefaultAsync(r => r.ReservaId == reservaId);

            if (reserva == null || reserva.Status != StatusReserva.Pendente)
                return null;

            if (reserva.Apartamento == null || reserva.Apartamento.Status != StatusApartamento.Disponivel)
                return "Apartamento não está mais disponível para confirmar esta reserva.";

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

            return "Reserva confirmada e check-in realizado.";
        }

        public async Task<bool> CancelarAsync(int reservaId)
        {
            var reserva = await _context.Reservas.FindAsync(reservaId);
            if (reserva == null || reserva.Status != StatusReserva.Pendente) return false;

            reserva.Status = StatusReserva.Cancelada;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
