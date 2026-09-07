using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;
using SisHospedar.Services;

namespace SisHospedar.Controllers
{
    // PT: Controller com duas metades: hóspede cria o pedido de reserva; recepção
    //     confirma (o que gera o check-in) ou cancela. Por isso o filtro de sessão de
    //     funcionário é aplicado por método (Index/Confirmar/Cancelar), não na classe.
    //     As regras de negócio vivem em IReservaService — o controller só cuida de
    //     sessão/ModelState/View.
    // EN: A controller with two halves: the guest creates the booking request;
    //     reception confirms it (which triggers check-in) or cancels it. That's why the
    //     employee session filter is applied per-method (Index/Confirmar/Cancelar), not
    //     on the class. The business rules live in IReservaService — the controller
    //     only handles session/ModelState/View concerns.
    public class ReservaController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IReservaService _reservaService;

        public ReservaController(AppDbContext context, IReservaService reservaService)
        {
            _context = context;
            _reservaService = reservaService;
        }

        // ---- Área do CLIENTE: criar pedido de reserva ----
        [RequerHospede]
        public IActionResult Create()
        {
            ViewData["ApartamentoId"] = new SelectList(
                _context.Apartamentos.Where(a => a.Status == StatusApartamento.Disponivel).OrderBy(a => a.Numero),
                "ApartamentoId", "Numero");

            return View();
        }

        // PT: Cria a reserva como Pendente. A validação de datas e a revalidação de
        //     disponibilidade do apartamento acontecem dentro do service.
        // EN: Creates the booking as Pendente. Date validation and room availability
        //     re-validation happen inside the service.
        [RequerHospede]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ApartamentoId,DataEntrada,DataSaidaPrevista")] Reserva reserva)
        {
            reserva.HospedeId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (ModelState.IsValid)
            {
                var (sucesso, erro) = await _reservaService.CriarPedidoAsync(reserva);
                if (sucesso)
                {
                    TempData["Sucesso"] = "Reserva enviada! A recepção vai confirmar em breve.";
                    return RedirectToAction("MinhaConta", "Cliente");
                }
                ModelState.AddModelError("", erro!);
            }

            ViewData["ApartamentoId"] = new SelectList(
                _context.Apartamentos.Where(a => a.Status == StatusApartamento.Disponivel).OrderBy(a => a.Numero),
                "ApartamentoId", "Numero", reserva.ApartamentoId);
            return View(reserva);
        }

        // ---- Área do FUNCIONÁRIO (recepção): confirmar/cancelar reservas ----
        [RequerFuncionario]
        public async Task<IActionResult> Index()
        {
            var reservas = await _context.Reservas
                .Include(r => r.Hospede)
                .Include(r => r.Apartamento)
                .OrderBy(r => r.Status)
                .ThenBy(r => r.DataEntrada)
                .ToListAsync();

            return View(reservas);
        }

        // PT: Ponto de check-in: confirmar a reserva cria a Hospedagem de verdade e
        //     ocupa o apartamento (ver IReservaService.ConfirmarAsync).
        // EN: The check-in point: confirming the booking creates the actual Hospedagem
        //     and occupies the room (see IReservaService.ConfirmarAsync).
        [RequerFuncionario]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmar(int id)
        {
            var mensagem = await _reservaService.ConfirmarAsync(id);
            if (mensagem != null) TempData["Sucesso"] = mensagem;
            return RedirectToAction(nameof(Index));
        }

        [RequerFuncionario]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            if (await _reservaService.CancelarAsync(id))
                TempData["Sucesso"] = "Reserva cancelada.";
            return RedirectToAction(nameof(Index));
        }
    }
}
