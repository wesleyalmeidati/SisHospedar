using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Services
{
    // PT: Os logs desta camada registram apenas identificadores (ids, status), nunca
    //     nome, CPF, e-mail ou qualquer dado pessoal do hóspede — log é um destino de
    //     retenção longa e leitura ampla, então PII não entra. O que interessa aqui é
    //     reconstruir "o que aconteceu com esta estadia", e o id basta para isso.
    // EN: Logging in this layer records identifiers only (ids, statuses), never a guest's
    //     name, CPF, email or any personal data — logs are long-retained and widely
    //     readable, so PII stays out. What matters here is reconstructing "what happened
    //     to this stay", and the id is enough for that.
    public class HospedagemService : IHospedagemService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HospedagemService> _logger;

        public HospedagemService(AppDbContext context, ILogger<HospedagemService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Sucesso, string? Erro)> AbrirCheckInAsync(Hospedagem hospedagem)
        {
            var apartamento = await _context.Apartamentos.FindAsync(hospedagem.ApartamentoId);
            if (apartamento == null || apartamento.Status != StatusApartamento.Disponivel)
            {
                _logger.LogWarning(
                    "Check-in recusado: apartamento {ApartamentoId} em {Status}.",
                    hospedagem.ApartamentoId, apartamento?.Status.ToString() ?? "inexistente");
                return (false, "Apartamento indisponível para hospedagem.");
            }

            apartamento.Status = StatusApartamento.Ocupado;
            _context.Add(hospedagem);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Check-in concluído: hospedagem {HospedagemId}, apartamento {ApartamentoId}.",
                hospedagem.HospedagemId, hospedagem.ApartamentoId);
            return (true, null);
        }

        public async Task<ResultadoCheckOut> FinalizarCheckOutAsync(int hospedagemId)
        {
            var hospedagem = await _context.Hospedagens
                .Include(h => h.Apartamento)
                .FirstOrDefaultAsync(h => h.HospedagemId == hospedagemId);

            if (hospedagem == null)
            {
                _logger.LogWarning("Check-out solicitado para hospedagem inexistente {HospedagemId}.", hospedagemId);
                return ResultadoCheckOut.NaoEncontrada;
            }

            hospedagem.Finalizada = true;
            hospedagem.DataSaida = DateTime.Today;
            if (hospedagem.Apartamento != null)
                hospedagem.Apartamento.Status = StatusApartamento.Disponivel;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Check-out concluído: hospedagem {HospedagemId}, apartamento {ApartamentoId} liberado.",
                hospedagemId, hospedagem.ApartamentoId);
            return ResultadoCheckOut.Sucesso;
        }
    }
}
