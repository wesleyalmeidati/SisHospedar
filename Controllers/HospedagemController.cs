using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;
using SisHospedar.Services;

namespace SisHospedar.Controllers
{
    // PT: Gerencia o check-in (Create), consulta e o check-out (Finalizar) das
    //     estadias. As regras de negócio (ocupar/liberar Apartamento.Status) vivem em
    //     IHospedagemService — o controller só cuida de ModelState/View.
    // EN: Manages check-in (Create), lookup, and check-out (Finalizar) of stays. The
    //     business rules (occupying/freeing Apartamento.Status) live in
    //     IHospedagemService — the controller only handles ModelState/View concerns.
    [RequerFuncionario]
    public class HospedagemController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHospedagemService _hospedagemService;

        public HospedagemController(AppDbContext context, IHospedagemService hospedagemService)
        {
            _context = context;
            _hospedagemService = hospedagemService;
        }

        public async Task<IActionResult> Index()
        {
            var hospedagens = await _context.Hospedagens
                .Include(h => h.Hospede)
                .Include(h => h.Apartamento)
                .OrderByDescending(h => h.DataEntrada)
                .ToListAsync();
            return View(hospedagens);
        }

        public async Task<IActionResult> Details(int? id)
        {
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
            CarregarListas();
            return View();
        }

        // PT: Check-in feito diretamente pela recepção (sem Reserva prévia). A revalidação
        //     de disponibilidade do apartamento acontece dentro do service.
        // EN: Check-in performed directly by reception (no prior Reserva). Room
        //     availability re-validation happens inside the service.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("HospedeId,ApartamentoId,DataEntrada")] Hospedagem hospedagem)
        {
            if (ModelState.IsValid)
            {
                var (sucesso, erro) = await _hospedagemService.AbrirCheckInAsync(hospedagem);
                if (sucesso)
                {
                    TempData["Sucesso"] = "Hospedagem registrada com sucesso.";
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError("", erro!);
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
        //     apartamento de volta para Disponivel (ver IHospedagemService).
        // EN: The actual check-out: marks the stay as finalized and frees the room
        //     back to Disponivel (see IHospedagemService).
        [HttpPost, ActionName("Finalizar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinalizarConfirmado(int id)
        {
            var resultado = await _hospedagemService.FinalizarCheckOutAsync(id);
            if (resultado == ResultadoCheckOut.NaoEncontrada) return NotFound();

            TempData["Sucesso"] = "Hospedagem finalizada com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        private void CarregarListas()
        {
            ViewData["HospedeId"] = new SelectList(_context.Hospedes.OrderBy(h => h.Nome), "HospedeId", "Nome");
            // PT: Mesma descrição do dropdown do hóspede — a recepção também precisa saber
            //     se o quarto é de casal ao abrir uma hospedagem no balcão. ToList() antes
            //     porque DescricaoParaSelecao é [NotMapped] e não traduz para SQL.
            // EN: The same description as the guest dropdown — reception also needs to know
            //     whether the room is a double when opening a stay at the counter. ToList()
            //     first because DescricaoParaSelecao is [NotMapped] and doesn't translate to SQL.
            ViewData["ApartamentoId"] = new SelectList(
                _context.Apartamentos
                    .Where(a => a.Status == StatusApartamento.Disponivel)
                    .OrderBy(a => a.Numero)
                    .ToList(),
                "ApartamentoId", nameof(Apartamento.DescricaoParaSelecao));
        }
    }
}
