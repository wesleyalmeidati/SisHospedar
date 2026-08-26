using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    public class FuncionarioController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<Funcionario> _hasher = new();

        public FuncionarioController(AppDbContext context) => _context = context;

        // Só Administrador e Gerente podem gerenciar a equipe.
        // Sem login de funcionário -> manda pro login. Logado mas sem permissão -> nega.
        private IActionResult? VerificarPermissao()
        {
            var funcionarioId = HttpContext.Session.GetInt32("FuncionarioId");
            if (funcionarioId == null) return RedirectToAction("Login", "Home");

            var cargo = HttpContext.Session.GetString("FuncionarioCargo");
            if (cargo != CargoFuncionario.Administrador.ToString() && cargo != CargoFuncionario.Gerente.ToString())
                return StatusCode(403, "Acesso negado: apenas Administrador ou Gerente podem gerenciar funcionários.");

            return null; // null = permissão concedida, segue em frente
        }

        public async Task<IActionResult> Index()
        {
            var bloqueio = VerificarPermissao();
            if (bloqueio != null) return bloqueio;

            return View(await _context.Funcionarios.OrderBy(f => f.Nome).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            var bloqueio = VerificarPermissao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var funcionario = await _context.Funcionarios.FirstOrDefaultAsync(f => f.FuncionarioId == id);
            if (funcionario == null) return NotFound();
            return View(funcionario);
        }

        public IActionResult Create()
        {
            var bloqueio = VerificarPermissao();
            if (bloqueio != null) return bloqueio;
            return View();
        }

        // Recebe a senha em texto puro APENAS neste ViewModel de entrada;
        // ela é convertida em hash antes de ser salva e nunca é persistida em claro.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Funcionario funcionario, string senha)
        {
            var bloqueio = VerificarPermissao();
            if (bloqueio != null) return bloqueio;

            ModelState.Remove(nameof(Funcionario.SenhaHash));
            if (string.IsNullOrWhiteSpace(senha) || senha.Length < 6)
                ModelState.AddModelError("senha", "A senha deve ter ao menos 6 caracteres.");

            if (ModelState.IsValid)
            {
                funcionario.SenhaHash = _hasher.HashPassword(funcionario, senha);
                _context.Add(funcionario);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Funcionário cadastrado com sucesso.";
                return RedirectToAction(nameof(Index));
            }
            return View(funcionario);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            var bloqueio = VerificarPermissao();
            if (bloqueio != null) return bloqueio;

            if (id == null) return NotFound();
            var funcionario = await _context.Funcionarios.FirstOrDefaultAsync(f => f.FuncionarioId == id);
            if (funcionario == null) return NotFound();
            return View(funcionario);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bloqueio = VerificarPermissao();
            if (bloqueio != null) return bloqueio;

            var funcionario = await _context.Funcionarios.FindAsync(id);
            if (funcionario != null)
            {
                _context.Funcionarios.Remove(funcionario);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Funcionário removido.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
