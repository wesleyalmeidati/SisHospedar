using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(AppDbContext context, ILogger<AnalyticsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RegistrarVisitasAsync(string caminho, int total, int recorrentes)
        {
            if (total <= 0) return;
            var hoje = DateTime.Today;

            // PT: Mesmo padrão do decremento de estoque em ConsumoService: um UPDATE
            //     executado no banco, não "ler, somar em memória e salvar". Duas requisições
            //     simultâneas na mesma página perderiam uma contagem no caminho ingênuo
            //     (TOCTOU); com ExecuteUpdateAsync o incremento acontece dentro do próprio
            //     UPDATE e o banco serializa.
            // EN: Same pattern as the stock decrement in ConsumoService: an UPDATE executed
            //     in the database, not "read, add in memory, save". Two concurrent requests
            //     to the same page would lose a count the naive way (TOCTOU); with
            //     ExecuteUpdateAsync the increment happens inside the UPDATE itself and the
            //     database serializes it.
            var linhas = await _context.VisitasPagina
                .Where(v => v.Caminho == caminho && v.Data == hoje)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.Total, v => v.Total + total)
                    .SetProperty(v => v.TotalRecorrentes, v => v.TotalRecorrentes + recorrentes));

            if (linhas > 0) return;

            // PT: Primeira visita a este caminho hoje: a linha ainda não existe. O índice
            //     único em (Caminho, Data) faz o banco recusar a segunda inserção se duas
            //     requisições chegarem juntas — por isso o catch, que então apenas repete o
            //     UPDATE, agora com a linha já criada pela concorrente.
            // EN: First visit to this path today: the row doesn't exist yet. The unique index
            //     on (Caminho, Data) makes the database reject the second insert if two
            //     requests arrive together — hence the catch, which then simply retries the
            //     UPDATE, now that the competing request has created the row.
            try
            {
                _context.VisitasPagina.Add(new VisitaPagina
                {
                    Caminho = caminho,
                    Data = hoje,
                    Total = total,
                    TotalRecorrentes = recorrentes
                });
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                await _context.VisitasPagina
                    .Where(v => v.Caminho == caminho && v.Data == hoje)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(v => v.Total, v => v.Total + total)
                        .SetProperty(v => v.TotalRecorrentes, v => v.TotalRecorrentes + recorrentes));
            }
        }

        public async Task RegistrarConsentimentoAsync(string identificadorAnonimo, bool aceito)
        {
            _context.ConsentimentosCookie.Add(new ConsentimentoCookie
            {
                IdentificadorAnonimo = identificadorAnonimo,
                Aceito = aceito,
                VersaoPolitica = Medicao.VersaoPolitica,
                DataRegistro = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // PT: Registro em log além da tabela: a tabela é a prova formal, o log é o que
            //     permite ver a proporção aceite/recusa sem consultar o banco. Nenhum dos
            //     dois guarda IP.
            // EN: Logged in addition to the table: the table is the formal proof, the log is
            //     what makes the accept/refuse ratio visible without querying the database.
            //     Neither stores an IP.
            _logger.LogInformation("Consentimento de medição registrado: {Decisao}.", aceito ? "aceito" : "recusado");
        }

        public async Task<IReadOnlyList<VisitaResumo>> ResumoAsync(int dias)
        {
            var desde = DateTime.Today.AddDays(-dias);
            return await _context.VisitasPagina
                .AsNoTracking()
                .Where(v => v.Data >= desde)
                .GroupBy(v => v.Caminho)
                .Select(g => new VisitaResumo(
                    g.Key,
                    g.Sum(v => v.Total),
                    g.Sum(v => v.TotalRecorrentes)))
                .OrderByDescending(r => r.Total)
                .Take(15)
                .ToListAsync();
        }

        public async Task<int> TotalNoPeriodoAsync(int dias)
        {
            var desde = DateTime.Today.AddDays(-dias);
            return await _context.VisitasPagina
                .AsNoTracking()
                .Where(v => v.Data >= desde)
                .SumAsync(v => (int?)v.Total) ?? 0;
        }
    }
}
