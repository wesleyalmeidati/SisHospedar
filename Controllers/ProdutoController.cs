using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Só funcionários logados podem gerenciar produtos (preço, estoque).
    // EN: Only logged-in employees can manage products (price, stock).
    [RequerFuncionario]
    public class ProdutoController : Controller
    {
        private readonly AppDbContext _context;
        public ProdutoController(AppDbContext context) => _context = context;

        public async Task<IActionResult> Index() =>
            View(await _context.Produtos.OrderBy(p => p.Nome).ToListAsync());

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.ProdutoId == id);
            if (produto == null) return NotFound();
            return View(produto);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nome,Preco,Estoque")] Produto produto)
        {
            if (ModelState.IsValid)
            {
                _context.Add(produto);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Produto cadastrado com sucesso.";
                return RedirectToAction(nameof(Index));
            }
            return View(produto);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return NotFound();
            return View(produto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProdutoId,Nome,Preco,Estoque")] Produto produto)
        {
            if (id != produto.ProdutoId) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(produto);
                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = "Produto atualizado com sucesso.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Produtos.Any(p => p.ProdutoId == id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(produto);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.ProdutoId == id);
            if (produto == null) return NotFound();
            return View(produto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return RedirectToAction(nameof(Index));

            // PT: Consumo → Produto é Restrict (Consumo → Hospedagem é que cascateia), então
            //     produto já consumido não sai: o banco recusa e o usuário recebia 500.
            //     A restrição é proposital — apagar o produto apagaria a descrição do que o
            //     hóspede consumiu de uma conta já fechada. Para tirar de linha, zere o
            //     estoque.
            // EN: Consumo → Produto is Restrict (it's Consumo → Hospedagem that cascades), so
            //     a product already consumed can't go: the database refuses and the user got
            //     a 500. The restriction is deliberate — deleting the product would erase the
            //     description of what a guest consumed on an already-closed bill. To
            //     discontinue it, set the stock to zero.
            var jaConsumido = await _context.Consumos.AnyAsync(c => c.ProdutoId == id);

            if (jaConsumido)
            {
                TempData["Erro"] = "Este produto já foi consumido por algum hóspede e não pode ser removido, " +
                                   "porque isso apagaria o histórico de consumo. Para tirá-lo de linha, zere o estoque.";
                return RedirectToAction(nameof(Index));
            }

            _context.Produtos.Remove(produto);
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Produto removido.";
            return RedirectToAction(nameof(Index));
        }
    }
}
