using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Authentication;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Página pública do hotel e login de funcionário (identidade separada do hóspede).
    // EN: The hotel's public landing page and employee login (identity separate from guests).
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

        // PT: Verifica a senha com PasswordHasher (nunca compara texto puro) e, se válida,
        //     assina o cookie de autenticação do esquema Funcionario com as claims de
        //     identidade (Id/Nome) e de Cargo (usada pelo [RequerCargo] via Roles).
        // EN: Verifies the password with PasswordHasher (never plain-text comparison) and,
        //     if valid, signs in the Funcionario cookie scheme with identity claims
        //     (Id/Nome) and the Cargo claim (used by [RequerCargo] via Roles).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string login, string senha)
        {
            var funcionario = await _context.Funcionarios.FirstOrDefaultAsync(f => f.Login == login);

            if (funcionario != null &&
                _hasher.VerifyHashedPassword(funcionario, funcionario.SenhaHash, senha) == PasswordVerificationResult.Success)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, funcionario.FuncionarioId.ToString()),
                    new(ClaimTypes.Name, funcionario.Nome),
                    new(ClaimTypes.Role, funcionario.Cargo.ToString())
                };
                var identidade = new ClaimsIdentity(claims, Esquemas.Funcionario);
                await HttpContext.SignInAsync(Esquemas.Funcionario, new ClaimsPrincipal(identidade));
                return RedirectToAction("Index", "Painel");
            }

            ModelState.AddModelError("", "Login ou senha inválidos.");
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(Esquemas.Funcionario);
            return RedirectToAction(nameof(Index));
        }
    }
}
