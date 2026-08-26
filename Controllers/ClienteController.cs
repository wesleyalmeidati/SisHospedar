using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    public class ClienteController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<Hospede> _hasher = new();

        public ClienteController(AppDbContext context) => _context = context;

        public IActionResult Registrar() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(Hospede hospede, string senha)
        {
            ModelState.Remove(nameof(Hospede.SenhaHash));

            if (string.IsNullOrWhiteSpace(senha) || senha.Length < 6)
                ModelState.AddModelError("senha", "A senha deve ter ao menos 6 caracteres.");

            if (await _context.Hospedes.AnyAsync(h => h.Login == hospede.Login))
                ModelState.AddModelError("Login", "Este login já está em uso.");

            if (await _context.Hospedes.AnyAsync(h => h.Cpf == hospede.Cpf))
                ModelState.AddModelError("Cpf", "Já existe uma conta com este CPF.");

            if (ModelState.IsValid)
            {
                hospede.SenhaHash = _hasher.HashPassword(hospede, senha);
                _context.Add(hospede);
                await _context.SaveChangesAsync();

                HttpContext.Session.SetInt32("HospedeId", hospede.HospedeId);
                HttpContext.Session.SetString("HospedeNome", hospede.Nome);

                TempData["Sucesso"] = "Conta criada com sucesso! Bem-vindo(a) ao Hotel Solaris.";
                return RedirectToAction("Create", "Reserva");
            }
            return View(hospede);
        }

        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string login, string senha)
        {
            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.Login == login);

            if (hospede != null && !string.IsNullOrEmpty(hospede.SenhaHash) &&
                _hasher.VerifyHashedPassword(hospede, hospede.SenhaHash, senha) == PasswordVerificationResult.Success)
            {
                HttpContext.Session.SetInt32("HospedeId", hospede.HospedeId);
                HttpContext.Session.SetString("HospedeNome", hospede.Nome);
                return RedirectToAction(nameof(MinhaConta));
            }

            ModelState.AddModelError("", "Login ou senha inválidos.");
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Remove("HospedeId");
            HttpContext.Session.Remove("HospedeNome");
            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> MinhaConta()
        {
            var hospedeId = HttpContext.Session.GetInt32("HospedeId");
            if (hospedeId == null) return RedirectToAction(nameof(Login));

            var hospedagemAtiva = await _context.Hospedagens
                .Include(h => h.Apartamento)
                .Include(h => h.Consumos).ThenInclude(c => c.Produto)
                .Where(h => h.HospedeId == hospedeId && !h.Finalizada)
                .FirstOrDefaultAsync();

            var reservasPendentes = await _context.Reservas
                .Include(r => r.Apartamento)
                .Where(r => r.HospedeId == hospedeId && r.Status == StatusReserva.Pendente)
                .ToListAsync();

            ViewData["Reservas"] = reservasPendentes;
            return View(hospedagemAtiva);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Prorrogar(int hospedagemId, DateTime novaDataSaida)
        {
            var hospedeId = HttpContext.Session.GetInt32("HospedeId");
            if (hospedeId == null) return RedirectToAction(nameof(Login));

            var hospedagem = await _context.Hospedagens
                .FirstOrDefaultAsync(h => h.HospedagemId == hospedagemId && h.HospedeId == hospedeId);

            if (hospedagem != null && !hospedagem.Finalizada && novaDataSaida > hospedagem.DataEntrada)
            {
                hospedagem.DataSaida = novaDataSaida;
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Estadia prorrogada com sucesso.";
            }
            return RedirectToAction(nameof(MinhaConta));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarEncerramento(int hospedagemId)
        {
            var hospedeId = HttpContext.Session.GetInt32("HospedeId");
            if (hospedeId == null) return RedirectToAction(nameof(Login));

            var hospedagem = await _context.Hospedagens
                .FirstOrDefaultAsync(h => h.HospedagemId == hospedagemId && h.HospedeId == hospedeId);

            if (hospedagem != null && !hospedagem.Finalizada)
            {
                hospedagem.SolicitouEncerramento = true;
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Pedido de encerramento enviado à recepção.";
            }
            return RedirectToAction(nameof(MinhaConta));
        }
    }
}
