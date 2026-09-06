using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;
using SisHospedar.Services;

namespace SisHospedar.Controllers
{
    // PT: Lança consumo de produtos durante uma hospedagem ativa. A validação e a
    //     baixa atômica de estoque vivem em IConsumoService — o controller só cuida
    //     de ModelState/View.
    // EN: Records product consumption during an active stay. Validation and the
    //     atomic stock deduction live in IConsumoService — the controller only
    //     handles ModelState/View concerns.
    [RequerFuncionario]
    public class ConsumoController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConsumoService _consumoService;

        public ConsumoController(AppDbContext context, IConsumoService consumoService)
        {
            _context = context;
            _consumoService = consumoService;
        }

        public IActionResult Create(int hospedagemId)
        {
            ViewData["HospedagemId"] = hospedagemId;
            ViewData["ProdutoId"] = new SelectList(_context.Produtos.OrderBy(p => p.Nome), "ProdutoId", "Nome");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("HospedagemId,ProdutoId,Quantidade")] Consumo consumo)
        {
            if (ModelState.IsValid)
            {
                var (sucesso, erro) = await _consumoService.RegistrarAsync(consumo);
                if (sucesso)
                {
                    TempData["Sucesso"] = "Consumo lançado com sucesso.";
                    return RedirectToAction("Details", "Hospedagem", new { id = consumo.HospedagemId });
                }
                ModelState.AddModelError("", erro!);
            }

            ViewData["HospedagemId"] = consumo.HospedagemId;
            ViewData["ProdutoId"] = new SelectList(_context.Produtos.OrderBy(p => p.Nome), "ProdutoId", "Nome", consumo.ProdutoId);
            return View(consumo);
        }
    }
}
