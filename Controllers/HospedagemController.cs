using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
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

        // Equivalente moderno da tela WFFinalizarHospedagem.aspx do projeto original.
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
