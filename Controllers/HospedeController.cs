using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Só funcionários logados podem ver/gerenciar hóspedes (dados pessoais: CPF, telefone...).
    // EN: Only logged-in employees can view/manage guests (personal data: CPF, phone...).
    [RequerFuncionario]
    public class HospedeController : Controller
    {
        private readonly AppDbContext _context;

        public HospedeController(AppDbContext context) => _context = context;

        // GET: /Hospede?busca=texto
        public async Task<IActionResult> Index(string? busca)
        {
            var query = _context.Hospedes.AsQueryable();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                query = query.Where(h => h.Nome.Contains(busca) || h.Cpf.Contains(busca));
            }

            ViewData["Busca"] = busca;
            return View(await query.OrderBy(h => h.Nome).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.HospedeId == id);
            if (hospede == null) return NotFound();
            return View(hospede);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nome,Cpf,Telefone,DataNascimento")] Hospede hospede)
        {
            if (ModelState.IsValid)
            {
                _context.Add(hospede);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Hóspede cadastrado com sucesso.";
                return RedirectToAction(nameof(Index));
            }
            return View(hospede);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var hospede = await _context.Hospedes.FindAsync(id);
            if (hospede == null) return NotFound();
            return View(hospede);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("HospedeId,Nome,Cpf,Telefone,DataNascimento")] Hospede hospede)
        {
            if (id != hospede.HospedeId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(hospede);
                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = "Hóspede atualizado com sucesso.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Hospedes.Any(h => h.HospedeId == id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(hospede);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.HospedeId == id);
            if (hospede == null) return NotFound();
            return View(hospede);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hospede = await _context.Hospedes.FindAsync(id);
            if (hospede != null)
            {
                _context.Hospedes.Remove(hospede);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Hóspede removido.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
