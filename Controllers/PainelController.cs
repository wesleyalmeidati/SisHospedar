using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;
using SisHospedar.Services;

namespace SisHospedar.Controllers
{
    // PT: Painel inicial do funcionário após o login — o conteúdo exibido muda
    //     conforme o Cargo (ver switch abaixo).
    // EN: The employee's landing dashboard after login — the content shown
    //     changes based on their Cargo (role, see switch below).
    [RequerFuncionario]
    public class PainelController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IAnalyticsService _analytics;

        public PainelController(AppDbContext context, IAnalyticsService analytics)
        {
            _context = context;
            _analytics = analytics;
        }

        public async Task<IActionResult> Index()
        {
            // PT: [RequerFuncionario] já garante que a claim existe e é um int válido.
            // EN: [RequerFuncionario] already guarantees the claim exists and is a valid int.
            var funcionarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var funcionario = await _context.Funcionarios.FindAsync(funcionarioId);
            if (funcionario == null) return RedirectToAction("Login", "Home");

            ViewData["Cargo"] = funcionario.Cargo;

            switch (funcionario.Cargo)
            {
                case CargoFuncionario.Administrador:
                case CargoFuncionario.Gerente:
                    ViewData["TotalApartamentos"] = await _context.Apartamentos.CountAsync();
                    ViewData["ApartamentosOcupados"] = await _context.Apartamentos.CountAsync(a => a.Status == StatusApartamento.Ocupado);
                    ViewData["ReservasPendentes"] = await _context.Reservas.CountAsync(r => r.Status == StatusReserva.Pendente);
                    ViewData["TotalFuncionarios"] = await _context.Funcionarios.CountAsync();
                    ViewData["ChegadasHoje"] = await ObterChegadasHojeAsync();

                    // PT: Audiência do site é informação de gestão, então aparece só para
                    //     Administrador/Gerente. São números agregados por página — não há
                    //     nada aqui sobre um visitante específico, nem haveria como: o que
                    //     está no banco são contadores, não registros individuais.
                    // EN: Site audience is management information, so it's shown only to
                    //     Administrador/Gerente. These are per-page aggregates — there is
                    //     nothing here about a specific visitor, nor could there be: what's
                    //     in the database are counters, not individual records.
                    ViewData["VisitasTotal"] = await _analytics.TotalNoPeriodoAsync(30);
                    ViewData["VisitasPorPagina"] = await _analytics.ResumoAsync(30);
                    break;

                case CargoFuncionario.Recepcionista:
                    ViewData["ReservasPendentes"] = await _context.Reservas.CountAsync(r => r.Status == StatusReserva.Pendente);
                    ViewData["PedidosEncerramento"] = await _context.Hospedagens.CountAsync(h => h.SolicitouEncerramento && !h.Finalizada);
                    ViewData["HospedagensAtivas"] = await _context.Hospedagens.CountAsync(h => !h.Finalizada);
                    ViewData["ChegadasHoje"] = await ObterChegadasHojeAsync();
                    break;

                case CargoFuncionario.Limpeza:
                case CargoFuncionario.Manutencao:
                    ViewData["Apartamentos"] = await _context.Apartamentos.OrderBy(a => a.Numero).ToListAsync();
                    break;

                case CargoFuncionario.Vallet:
                    ViewData["HospedagensAtivas"] = await _context.Hospedagens
                        .Include(h => h.Hospede)
                        .Include(h => h.Apartamento)
                        .Where(h => !h.Finalizada)
                        .ToListAsync();
                    break;
            }

            return View();
        }

        // PT: Reservas pendentes cuja data de entrada é hoje — comparado com a data do
        //     próprio servidor (DateTime.Today), sem depender de nenhum relógio externo.
        //     É o alerta "chega hóspede hoje" pedido pra recepção/gerência não perder
        //     nenhuma chegada esquecida numa lista de pendentes sem ordenação por data.
        // EN: Pending reservations whose check-in date is today — compared against the
        //     server's own clock (DateTime.Today), no external time source involved.
        //     This is the "guest arriving today" alert reception/management asked for,
        //     so no arrival gets missed in an unsorted list of pending requests.
        private async Task<List<Reserva>> ObterChegadasHojeAsync() =>
            await _context.Reservas
                .Include(r => r.Hospede)
                .Include(r => r.Apartamento)
                .Where(r => r.Status == StatusReserva.Pendente && r.DataEntrada == DateTime.Today)
                .OrderBy(r => r.Hospede!.Nome)
                .ToListAsync();
    }
}
