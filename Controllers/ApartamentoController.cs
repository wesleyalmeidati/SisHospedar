using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Só funcionários logados podem gerenciar apartamentos (preço, status, etc).
    // EN: Only logged-in employees can manage rooms (price, status, etc).
    [RequerFuncionario]
    public class ApartamentoController : Controller
    {
        private readonly AppDbContext _context;
        public ApartamentoController(AppDbContext context) => _context = context;

        public async Task<IActionResult> Index() =>
            View(await _context.Apartamentos.OrderBy(a => a.Numero).ToListAsync());

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var apartamento = await _context.Apartamentos.FirstOrDefaultAsync(a => a.ApartamentoId == id);
            if (apartamento == null) return NotFound();
            return View(apartamento);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Numero,Capacidade,ValorDiaria,Status")] Apartamento apartamento)
        {
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
            if (id == null) return NotFound();
            var apartamento = await _context.Apartamentos.FindAsync(id);
            if (apartamento == null) return NotFound();
            return View(apartamento);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ApartamentoId,Numero,Capacidade,ValorDiaria,Status")] Apartamento apartamento)
        {
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
            if (id == null) return NotFound();
            var apartamento = await _context.Apartamentos.FirstOrDefaultAsync(a => a.ApartamentoId == id);
            if (apartamento == null) return NotFound();
            return View(apartamento);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var apartamento = await _context.Apartamentos.FindAsync(id);
            if (apartamento == null) return RedirectToAction(nameof(Index));

            // PT: Mesmo caso de HospedeController: as FKs de Reserva e Hospedagem para
            //     Apartamento são Restrict, então o banco recusa o DELETE e antes disto o
            //     usuário recebia uma página 500. Um apartamento que já foi usado nunca
            //     poderá ser excluído — o caminho correto para tirá-lo de operação é mudar
            //     o Status para Manutencao, que já existe no modelo e o remove das listas de
            //     disponíveis.
            // EN: Same case as HospedeController: the Reserva and Hospedagem FKs to
            //     Apartamento are Restrict, so the database refuses the DELETE and the user
            //     previously got a 500 page. A room that has been used can never be deleted —
            //     the correct way to take it out of service is changing its Status to
            //     Manutencao, which already exists on the model and drops it from the
            //     available lists.
            var emUso = await _context.Reservas.AnyAsync(r => r.ApartamentoId == id)
                     || await _context.Hospedagens.AnyAsync(h => h.ApartamentoId == id);

            if (emUso)
            {
                TempData["Erro"] = "Este apartamento tem reservas ou hospedagens registradas e não pode ser removido. " +
                                   "Para tirá-lo de operação, mude o status para Manutenção.";
                return RedirectToAction(nameof(Index));
            }

            _context.Apartamentos.Remove(apartamento);
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Apartamento removido.";
            return RedirectToAction(nameof(Index));
        }
    }
}
