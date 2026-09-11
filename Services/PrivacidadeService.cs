using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Services
{
    // PT: Concentra os direitos do titular previstos na LGPD que exigem tocar o banco:
    //     acesso/portabilidade (Art. 18, II e V) e eliminação (Art. 18, VI). Fica em
    //     Services/ pela mesma razão que check-in e baixa de estoque ficam: é regra de
    //     negócio com efeito colateral, e precisa ser testável sem passar pelo HTTP.
    // EN: Holds the LGPD data-subject rights that need to touch the database: access and
    //     portability (Art. 18, II and V) and erasure (Art. 18, VI). It lives in Services/
    //     for the same reason check-in and stock decrement do: it's business logic with
    //     side effects, and it has to be testable without going through HTTP.
    public class PrivacidadeService : IPrivacidadeService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PrivacidadeService> _logger;

        public PrivacidadeService(AppDbContext context, ILogger<PrivacidadeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DadosPessoaisHospede?> ReunirDadosAsync(int hospedeId)
        {
            var hospede = await _context.Hospedes
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.HospedeId == hospedeId);
            if (hospede == null) return null;

            var reservas = await _context.Reservas
                .AsNoTracking()
                .Include(r => r.Apartamento)
                .Where(r => r.HospedeId == hospedeId)
                .OrderBy(r => r.DataEntrada)
                .Select(r => new ReservaExportada(
                    r.ReservaId,
                    r.Apartamento!.Numero,
                    r.DataEntrada,
                    r.DataSaidaPrevista,
                    r.Status.ToString()))
                .ToListAsync();

            var hospedagens = await _context.Hospedagens
                .AsNoTracking()
                .Include(h => h.Apartamento)
                .Include(h => h.Consumos).ThenInclude(c => c.Produto)
                .Where(h => h.HospedeId == hospedeId)
                .OrderBy(h => h.DataEntrada)
                .ToListAsync();

            var hospedagensExportadas = hospedagens
                .Select(h => new HospedagemExportada(
                    h.HospedagemId,
                    h.Apartamento?.Numero ?? "-",
                    h.DataEntrada,
                    h.DataSaida,
                    h.Finalizada,
                    h.Consumos.Select(c => new ConsumoExportado(
                        c.Produto?.Nome ?? "-",
                        c.Quantidade,
                        c.ValorUnitario,
                        c.ValorUnitario * c.Quantidade)).ToList()))
                .ToList();

            return new DadosPessoaisHospede(
                hospede.HospedeId,
                hospede.Nome,
                hospede.Cpf,
                hospede.Telefone,
                hospede.DataNascimento,
                hospede.Email,
                hospede.Login,
                reservas,
                hospedagensExportadas);
        }

        public async Task<ResultadoExclusao> ExcluirContaAsync(int hospedeId)
        {
            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.HospedeId == hospedeId);
            if (hospede == null) return ResultadoExclusao.NaoEncontrado;

            // PT: Uma estadia em aberto é uma relação contratual em curso — o hotel ainda
            //     precisa identificar quem está no quarto. Recusar aqui é mais honesto do
            //     que anonimizar e deixar a recepção sem saber quem hospedou.
            // EN: An open stay is an ongoing contractual relationship — the hotel still
            //     needs to know who is in the room. Refusing here is more honest than
            //     anonymizing and leaving reception unable to identify the occupant.
            if (await _context.Hospedagens.AnyAsync(h => h.HospedeId == hospedeId && !h.Finalizada))
                return ResultadoExclusao.BloqueadoEstadiaAtiva;

            if (await _context.Reservas.AnyAsync(r => r.HospedeId == hospedeId && r.Status == StatusReserva.Pendente))
                return ResultadoExclusao.BloqueadoReservaPendente;

            var temHistorico = await _context.Hospedagens.AnyAsync(h => h.HospedeId == hospedeId)
                            || await _context.Reservas.AnyAsync(r => r.HospedeId == hospedeId);

            if (!temHistorico)
            {
                // PT: Nunca se hospedou nem reservou: não há registro de hospedagem nem
                //     lançamento fiscal a preservar, então a linha pode sair de verdade.
                // EN: Never stayed and never booked: there's no stay record or financial
                //     entry to preserve, so the row can genuinely go.
                _context.Hospedes.Remove(hospede);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Conta de hóspede {HospedeId} excluída a pedido do titular (sem histórico).", hospedeId);
                return ResultadoExclusao.Excluido;
            }

            // PT: Há histórico. O Art. 16 da LGPD permite reter o que é necessário para
            //     cumprir obrigação legal — no caso, o registro da hospedagem e seus
            //     lançamentos. Então descaracterizamos os dados pessoais e mantemos a
            //     casca transacional, que pelo Art. 12 deixa de ser dado pessoal.
            //     O CPF vira um marcador: a coluna é obrigatória e tem índice único sem
            //     filtro, então precisa de um valor único e não nulo — daí o id embutido.
            //     Ele tem 12 caracteres e a coluna aceita 14. Não passa no CpfValido, mas
            //     essa validação só roda em binding de formulário, nunca contra linha já
            //     gravada.
            // EN: There is history. LGPD Art. 16 allows retaining what's needed to comply
            //     with a legal obligation — here, the stay record and its entries. So we
            //     strip the personal data and keep the transactional shell, which Art. 12
            //     treats as no longer personal data.
            //     The CPF becomes a marker: the column is required and has an unfiltered
            //     unique index, so it needs a non-null unique value — hence the embedded
            //     id. It's 12 characters and the column takes 14. It won't pass CpfValido,
            //     but that validation only runs on form binding, never against a stored row.
            hospede.Nome = "Titular removido";
            hospede.Cpf = $"ANON{hospedeId:D8}";
            hospede.Telefone = string.Empty;
            hospede.DataNascimento = new DateTime(1900, 1, 1);
            hospede.Email = null;
            hospede.Login = null;
            hospede.SenhaHash = null;
            hospede.SecurityStamp = null;
            hospede.AccessFailedCount = 0;
            hospede.LockoutEnd = null;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Conta de hóspede {HospedeId} anonimizada a pedido do titular (histórico preservado).", hospedeId);
            return ResultadoExclusao.Anonimizado;
        }
    }
}
