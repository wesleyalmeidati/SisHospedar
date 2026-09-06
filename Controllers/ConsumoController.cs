using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Lança consumo de produtos durante uma hospedagem ativa, com baixa
    //     atômica no estoque.
    // EN: Records product consumption during an active stay, with an atomic
    //     stock deduction.
    public class ConsumoController : Controller
    {
        private readonly AppDbContext _context;
        public ConsumoController(AppDbContext context) => _context = context;

        // Só funcionários logados podem lançar consumo em uma hospedagem.
        private IActionResult? VerificarSessao()
        {
            if (HttpContext.Session.GetInt32("FuncionarioId") == null)
                return RedirectToAction("Login", "Home");
            return null;
        }

        public IActionResult Create(int hospedagemId)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            ViewData["HospedagemId"] = hospedagemId;
            ViewData["ProdutoId"] = new SelectList(_context.Produtos.OrderBy(p => p.Nome), "ProdutoId", "Nome");
            return View();
        }

        // PT: Valida hospedagem ativa e produto existente, copia o preço atual para
        //     ValorUnitario (histórico) e decrementa o estoque de forma atômica.
        // EN: Validates the stay is active and the product exists, copies the current
        //     price into ValorUnitario (price history), and atomically deducts stock.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("HospedagemId,ProdutoId,Quantidade")] Consumo consumo)
        {
            var bloqueio = VerificarSessao();
            if (bloqueio != null) return bloqueio;

            var hospedagem = await _context.Hospedagens.FindAsync(consumo.HospedagemId);
            if (hospedagem == null || hospedagem.Finalizada)
            {
                ModelState.AddModelError("", "Hospedagem inválida ou já finalizada.");
            }

            var produto = await _context.Produtos.FindAsync(consumo.ProdutoId);
            if (produto == null)
            {
                ModelState.AddModelError("", "Produto não encontrado.");
            }

            if (ModelState.IsValid && produto != null)
            {
                consumo.ValorUnitario = produto.Preco;

                // PT: Decremento atômico no banco (WHERE Estoque >= Quantidade) para evitar
                //     que duas requisições concorrentes deixem o estoque negativo (TOCTOU).
                // EN: Atomic DB decrement (WHERE Estoque >= Quantidade) to prevent two
                //     concurrent requests from pushing stock negative (TOCTOU race).
                var linhasAfetadas = await _context.Produtos
                    .Where(p => p.ProdutoId == consumo.ProdutoId && p.Estoque >= consumo.Quantidade)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estoque, p => p.Estoque - consumo.Quantidade));

                if (linhasAfetadas == 0)
                {
                    ModelState.AddModelError("", "Estoque insuficiente para este produto.");
                }
                else
                {
                    _context.Add(consumo);
                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = "Consumo lançado com sucesso.";
                    return RedirectToAction("Details", "Hospedagem", new { id = consumo.HospedagemId });
                }
            }

            ViewData["HospedagemId"] = consumo.HospedagemId;
            ViewData["ProdutoId"] = new SelectList(_context.Produtos.OrderBy(p => p.Nome), "ProdutoId", "Nome", consumo.ProdutoId);
            return View(consumo);
        }
    }
}
