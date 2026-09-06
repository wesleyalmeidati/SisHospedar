using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Gerencia o check-in (Create), consulta e o check-out (Finalizar) das
    //     estadias. Mantém Apartamento.Status sincronizado a cada transição.
    // EN: Manages check-in (Create), lookup, and check-out (Finalizar) of stays.
    //     Keeps Apartamento.Status in sync at every transition.
    public class HospedagemController : Controller
    {
        private readonly AppDbContext _context;
        public HospedagemController(AppDbContext context) => _context = context;

        // Só funcionários logados podem gerenciar hospedagens (check-in/check-out).
        private IActionResult? VerificarSessao()
        {
            if (HttpContext.Session.GetInt32("FuncionarioId") == null)
                return RedirectToAction("Login", "Home");
            return null;
        }

        public async Task<IActionResult> Index()
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            var hospedagens = await _context.Hospedagens
                .Include(h => h.Hospede)
                .Include(h => h.Apartamento)
                .OrderByDescending(h => h.DataEntrada)
                .ToListAsync();
            return View(hospedagens);
        }

        public async Task<IActionResult> Details(int? id)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var hospedagem = await _context.Hospedagens
                .Include(h => h.Hospede)
                .Include(h => h.Apartamento)
                .Include(h => h.Consumos).ThenInclude(c => c.Produto)
                .FirstOrDefaultAsync(h => h.HospedagemId == id);
            if (hospedagem == null) return NotFound();
            return View(hospedagem);
        }

        public IActionResult Create()
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            CarregarListas();
            return View();
        }

        // PT: Check-in feito diretamente pela recepção (sem Reserva prévia). Revalida
        //     no servidor se o apartamento ainda está Disponivel antes de ocupá-lo.
        // EN: Check-in performed directly by reception (no prior Reserva). Re-validates
        //     server-side that the room is still Disponivel before occupying it.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("HospedeId,ApartamentoId,DataEntrada")] Hospedagem hospedagem)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (ModelState.IsValid)
            {
                var apartamento = await _context.Apartamentos.FindAsync(hospedagem.ApartamentoId);
                if (apartamento == null || apartamento.Status != StatusApartamento.Disponivel)
                {
                    ModelState.AddModelError("", "Apartamento indisponível para hospedagem.");
                    CarregarListas();
                    return View(hospedagem);
                }

                apartamento.Status = StatusApartamento.Ocupado;
                _context.Add(hospedagem);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Hospedagem registrada com sucesso.";
                return RedirectToAction(nameof(Index));
            }
            CarregarListas();
            return View(hospedagem);
        }

        // PT: Equivalente moderno da tela WFFinalizarHospedagem.aspx do projeto original —
        //     tela de conferência antes do check-out (FinalizarConfirmado faz a baixa).
        // EN: Modern equivalent of the original project's WFFinalizarHospedagem.aspx —
        //     the review screen before check-out (FinalizarConfirmado does the actual work).
        public async Task<IActionResult> Finalizar(int? id)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var hospedagem = await _context.Hospedagens
                .Include(h => h.Hospede)
                .Include(h => h.Apartamento)
                .Include(h => h.Consumos).ThenInclude(c => c.Produto)
                .FirstOrDefaultAsync(h => h.HospedagemId == id);
            if (hospedagem == null || hospedagem.Finalizada) return NotFound();
            return View(hospedagem);
        }

        // PT: Check-out de fato: marca a hospedagem como finalizada e libera o
        //     apartamento de volta para Disponivel.
        // EN: The actual check-out: marks the stay as finalized and frees the
        //     room back to Disponivel.
        [HttpPost, ActionName("Finalizar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarConfirmado(int id)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            var hospedagem = await _context.Hospedagens
                .Include(h => h.Apartamento)
                .FirstOrDefaultAsync(h => h.HospedagemId == id);

            if (hospedagem == null) return NotFound();

            hospedagem.Finalizada = true;
            hospedagem.DataSaida = DateTime.Today;
            if (hospedagem.Apartamento != null)
                hospedagem.Apartamento.Status = StatusApartamento.Disponivel;

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Hospedagem finalizada com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        private void CarregarListas()
        {
            ViewData["HospedeId"] = new SelectList(_context.Hospedes.OrderBy(h => h.Nome), "HospedeId", "Nome");
            ViewData["ApartamentoId"] = new SelectList(
                _context.Apartamentos.Where(a => a.Status == StatusApartamento.Disponivel).OrderBy(a => a.Numero),
                "ApartamentoId", "Numero");
        }
    }
}
