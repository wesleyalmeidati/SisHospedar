using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    public class ProdutoController : Controller
    {
        private readonly AppDbContext _context;
        public ProdutoController(AppDbContext context) => _context = context;

        // Só funcionários logados podem gerenciar produtos (preço, estoque).
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
            return View(await _context.Produtos.OrderBy(p => p.Nome).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.ProdutoId == id);
            if (produto == null) return NotFound();
            return View(produto);
        }

        public IActionResult Create()
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nome,Preco,Estoque")] Produto produto)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

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
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var produto = await _context.Produtos.FindAsync(id);
            if (produto == null) return NotFound();
            return View(produto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ProdutoId,Nome,Preco,Estoque")] Produto produto)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

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
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.ProdutoId == id);
            if (produto == null) return NotFound();
            return View(produto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            var produto = await _context.Produtos.FindAsync(id);
            if (produto != null)
            {
                _context.Produtos.Remove(produto);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Produto removido.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
