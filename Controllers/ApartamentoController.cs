using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    public class ApartamentoController : Controller
    {
        private readonly AppDbContext _context;
        public ApartamentoController(AppDbContext context) => _context = context;

        // Só funcionários logados podem gerenciar apartamentos (preço, status, etc).
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
            return View(await _context.Apartamentos.OrderBy(a => a.Numero).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var apartamento = await _context.Apartamentos.FirstOrDefaultAsync(a => a.ApartamentoId == id);
            if (apartamento == null) return NotFound();
            return View(apartamento);
        }

        public IActionResult Create()
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Numero,Capacidade,ValorDiaria,Status")] Apartamento apartamento)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (ModelState.IsValid)
            {
                _context.Add(apartamento);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Apartamento cadastrado com sucesso.";
                return RedirectToAction(nameof(Index));
            }
            return View(apartamento);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var apartamento = await _context.Apartamentos.FindAsync(id);
            if (apartamento == null) return NotFound();
            return View(apartamento);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ApartamentoId,Numero,Capacidade,ValorDiaria,Status")] Apartamento apartamento)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (id != apartamento.ApartamentoId) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(apartamento);
                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = "Apartamento atualizado com sucesso.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Apartamentos.Any(a => a.ApartamentoId == id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(apartamento);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var apartamento = await _context.Apartamentos.FirstOrDefaultAsync(a => a.ApartamentoId == id);
            if (apartamento == null) return NotFound();
            return View(apartamento);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            var apartamento = await _context.Apartamentos.FindAsync(id);
            if (apartamento != null)
            {
                _context.Apartamentos.Remove(apartamento);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Apartamento removido.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
