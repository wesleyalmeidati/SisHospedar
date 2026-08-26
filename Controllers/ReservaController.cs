using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
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
        public async Task<IActionResult> Index()
        {
            if (HttpContext.Session.GetInt32("FuncionarioId") == null)
                return RedirectToAction("Login", "Home");

            var reservas = await _context.Reservas
                .Include(r => r.Hospede)
                .Include(r => r.Apartamento)
                .OrderBy(r => r.Status)
                .ThenBy(r => r.DataEntrada)
                .ToListAsync();

            return View(reservas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmar(int id)
        {
            if (HttpContext.Session.GetInt32("FuncionarioId") == null)
                return RedirectToAction("Login", "Home");

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

            // Confirmar a reserva = fazer o check-in (cria a Hospedagem de verdade)
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            if (HttpContext.Session.GetInt32("FuncionarioId") == null)
                return RedirectToAction("Login", "Home");

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
