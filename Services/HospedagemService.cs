using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Services
{
    public class HospedagemService : IHospedagemService
    {
        private readonly AppDbContext _context;
        public HospedagemService(AppDbContext context) => _context = context;

        public async Task<(bool Sucesso, string? Erro)> AbrirCheckInAsync(Hospedagem hospedagem)
        {
            var apartamento = await _context.Apartamentos.FindAsync(hospedagem.ApartamentoId);
            if (apartamento == null || apartamento.Status != StatusApartamento.Disponivel)
                return (false, "Apartamento indisponível para hospedagem.");

            apartamento.Status = StatusApartamento.Ocupado;
            _context.Add(hospedagem);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<ResultadoCheckOut> FinalizarCheckOutAsync(int hospedagemId)
        {
            var hospedagem = await _context.Hospedagens
                .Include(h => h.Apartamento)
                .FirstOrDefaultAsync(h => h.HospedagemId == hospedagemId);

            if (hospedagem == null) return ResultadoCheckOut.NaoEncontrada;

            hospedagem.Finalizada = true;
            hospedagem.DataSaida = DateTime.Today;
            if (hospedagem.Apartamento != null)
                hospedagem.Apartamento.Status = StatusApartamento.Disponivel;

            await _context.SaveChangesAsync();
            return ResultadoCheckOut.Sucesso;
        }
    }
}
