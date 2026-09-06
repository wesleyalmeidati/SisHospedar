using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Controller com duas metades: hóspede cria o pedido de reserva; recepção
    //     confirma (o que gera o check-in) ou cancela. Por isso o filtro de sessão de
    //     funcionário é aplicado por método (Index/Confirmar/Cancelar), não na classe.
    // EN: A controller with two halves: the guest creates the booking request;
    //     reception confirms it (which triggers check-in) or cancels it. That's why the
    //     employee session filter is applied per-method (Index/Confirmar/Cancelar), not on the class.
    public class ReservaController : Controller
    {
        private readonly AppDbContext _context;
        public ReservaController(AppDbContext context) => _context = context;

        // ---- Área do CLIENTE: criar pedido de reserva ----
        public IActionResult Create()
        {
            var hospedeId = HttpContext.Session.GetInt32("HospedeId");
            if (hospedeId == null) return RedirectToAction("Login", "Cliente");

            ViewData["ApartamentoId"] = new SelectList(
                _context.Apartamentos.Where(a => a.Status == StatusApartamento.Disponivel).OrderBy(a => a.Numero),
                "ApartamentoId", "Numero");

            return View();
        }

        // PT: Cria a reserva como Pendente. Revalida no servidor se o apartamento
        //     ainda está Disponivel (o dropdown pode estar desatualizado).
        // EN: Creates the booking as Pendente. Re-validates server-side that the
        //     room is still Disponivel (the dropdown may be stale).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ApartamentoId,DataEntrada,DataSaidaPrevista")] Reserva reserva)
        {
            var hospedeId = HttpContext.Session.GetInt32("HospedeId");
            if (hospedeId == null) return RedirectToAction("Login", "Cliente");

            reserva.HospedeId = hospedeId.Value;

            if (reserva.DataSaidaPrevista <= reserva.DataEntrada)
                ModelState.AddModelError("", "A data de saída deve ser depois da data de entrada.");

            if (reserva.DataEntrada < DateTime.Today)
                ModelState.AddModelError("", "A data de entrada não pode ser no passado.");

            if (ModelState.IsValid)
            {
                var apartamento = await _context.Apartamentos.FindAsync(reserva.ApartamentoId);
                if (apartamento == null || apartamento.Status != StatusApartamento.Disponivel)
                {
                    ModelState.AddModelError("", "Apartamento não está mais disponível.");
                }
                else
                {
                    reserva.Status = StatusReserva.Pendente;
                    _context.Add(reserva);
                    await _context.SaveChangesAsync();
                    TempData["Sucesso"] = "Reserva enviada! A recepção vai confirmar em breve.";
                    return RedirectToAction("MinhaConta", "Cliente");
                }
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
        //     ocupa o apartamento. Falha graciosamente se o quarto não estiver mais livre.
        // EN: The check-in point: confirming the booking creates the actual Hospedagem
        //     and occupies the room. Fails gracefully if the room is no longer free.
        [RequerFuncionario]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmar(int id)
        {
            var reserva = await _context.Reservas
                .Include(r => r.Apartamento)
                .FirstOrDefaultAsync(r => r.ReservaId == id);

            if (reserva == null || reserva.Status != StatusReserva.Pendente)
                return RedirectToAction(nameof(Index));

            if (reserva.Apartamento == null || reserva.Apartamento.Status != StatusApartamento.Disponivel)
            {
                TempData["Sucesso"] = "Apartamento não está mais disponível para confirmar esta reserva.";
                return RedirectToAction(nameof(Index));
            }

            // PT: Confirmar a reserva = fazer o check-in (cria a Hospedagem de verdade).
            // EN: Confirming the booking = performing check-in (creates the actual Hospedagem).
            var hospedagem = new Hospedagem
            {
                HospedeId = reserva.HospedeId,
                ApartamentoId = reserva.ApartamentoId,
                DataEntrada = reserva.DataEntrada,
                DataSaida = reserva.DataSaidaPrevista
            };

            reserva.Apartamento.Status = StatusApartamento.Ocupado;
            reserva.Status = StatusReserva.Confirmada;

            _context.Add(hospedagem);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Reserva confirmada e check-in realizado.";
            return RedirectToAction(nameof(Index));
        }

        [RequerFuncionario]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var reserva = await _context.Reservas.FindAsync(id);
            if (reserva != null && reserva.Status == StatusReserva.Pendente)
            {
                reserva.Status = StatusReserva.Cancelada;
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Reserva cancelada.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
