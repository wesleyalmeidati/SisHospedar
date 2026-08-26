using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<Funcionario> _hasher = new();

        public HomeController(AppDbContext context) => _context = context;

        // Página pública do hotel — visível para qualquer visitante, sem login.
        public async Task<IActionResult> Index()
        {
            var quartos = await _context.Apartamentos
                .Where(a => a.Status == StatusApartamento.Disponivel)
                .OrderBy(a => a.ValorDiaria)
                .Take(6)
                .ToListAsync();
            return View(quartos);
        }

        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string login, string senha)
        {
            var funcionario = await _context.Funcionarios.FirstOrDefaultAsync(f => f.Login == login);

            if (funcionario != null &&
                _hasher.VerifyHashedPassword(funcionario, funcionario.SenhaHash, senha) == PasswordVerificationResult.Success)
            {
                HttpContext.Session.SetInt32("FuncionarioId", funcionario.FuncionarioId);
                HttpContext.Session.SetString("FuncionarioNome", funcionario.Nome);
                HttpContext.Session.SetString("FuncionarioCargo", funcionario.Cargo.ToString());
                return RedirectToAction("Index", "Painel");
            }

            ModelState.AddModelError("", "Login ou senha inválidos.");
            return View();
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Index));
        }
    }
}

