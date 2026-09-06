using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Painel inicial do funcionário após o login — o conteúdo exibido muda
    //     conforme o Cargo (ver switch abaixo).
    // EN: The employee's landing dashboard after login — the content shown
    //     changes based on their Cargo (role, see switch below).
    public class PainelController : Controller
    {
        private readonly AppDbContext _context;
        public PainelController(AppDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var funcionarioId = HttpContext.Session.GetInt32("FuncionarioId");
            if (funcionarioId == null) return RedirectToAction("Login", "Home");

            var funcionario = await _context.Funcionarios.FindAsync(funcionarioId);
            if (funcionario == null) return RedirectToAction("Login", "Home");

            ViewData["Cargo"] = funcionario.Cargo;

            switch (funcionario.Cargo)
            {
                case CargoFuncionario.Administrador:
                case CargoFuncionario.Gerente:
                    ViewData["TotalApartamentos"] = await _context.Apartamentos.CountAsync();
                    ViewData["ApartamentosOcupados"] = await _context.Apartamentos.CountAsync(a => a.Status == StatusApartamento.Ocupado);
                    ViewData["ReservasPendentes"] = await _context.Reservas.CountAsync(r => r.Status == StatusReserva.Pendente);
                    ViewData["TotalFuncionarios"] = await _context.Funcionarios.CountAsync();
                    break;

                case CargoFuncionario.Recepcionista:
                    ViewData["ReservasPendentes"] = await _context.Reservas.CountAsync(r => r.Status == StatusReserva.Pendente);
                    ViewData["PedidosEncerramento"] = await _context.Hospedagens.CountAsync(h => h.SolicitouEncerramento && !h.Finalizada);
                    ViewData["HospedagensAtivas"] = await _context.Hospedagens.CountAsync(h => !h.Finalizada);
                    break;

                case CargoFuncionario.Limpeza:
                case CargoFuncionario.Manutencao:
                    ViewData["Apartamentos"] = await _context.Apartamentos.OrderBy(a => a.Numero).ToListAsync();
                    break;

                case CargoFuncionario.Vallet:
                    ViewData["HospedagensAtivas"] = await _context.Hospedagens
                        .Include(h => h.Hospede)
                        .Include(h => h.Apartamento)
                        .Where(h => !h.Finalizada)
                        .ToListAsync();
                    break;
            }

            return View();
        }
    }
}
