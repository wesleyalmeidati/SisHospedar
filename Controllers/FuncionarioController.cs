using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Gerencia a equipe do hotel. Restrito a Administrador/Gerente
    //     (ver VerificarPermissao abaixo).
    // EN: Manages hotel staff. Restricted to Administrador/Gerente roles
    //     (see VerificarPermissao below).
    public class FuncionarioController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<Funcionario> _hasher = new();

        public FuncionarioController(AppDbContext context) => _context = context;

        // PT: Só Administrador e Gerente podem gerenciar a equipe. Sem login de
        //     funcionário -> manda pro login. Logado mas sem permissão -> nega (403).
        // EN: Only Administrador and Gerente can manage staff. No employee session
        //     -> redirects to login. Logged in but wrong role -> denies (403).
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
            var funcionario = await _context.Funcionarios
                .Include(f => f.Hospede)
                .FirstOrDefaultAsync(f => f.FuncionarioId == id);
            if (funcionario == null) return NotFound();
            return View(funcionario);
        }

        public IActionResult Create()
        {
            var bloqueio = VerificarPermissao();
            if (bloqueio != null) return bloqueio;
            CarregarListaHospedes();
            return View();
        }

        // PT: Recebe a senha em texto puro APENAS neste parâmetro de entrada; ela é
        //     convertida em hash antes de ser salva e nunca é persistida em claro.
        //     HospedeId é opcional: só vincula se o novo funcionário já tiver conta de hóspede.
        //     Login é validado contra as duas tabelas para impedir colisão entre identidades.
        // EN: Receives the plain-text password ONLY as this input parameter; it's
        //     hashed before saving and is never persisted or logged in the clear.
        //     HospedeId is optional: only links if the new employee already has a guest account.
        //     Login is checked against both tables to prevent collisions between identities.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nome,Cargo,Login,HospedeId")] Funcionario funcionario, string senha)
        {
            var bloqueio = VerificarPermissao();
            if (bloqueio != null) return bloqueio;

            ModelState.Remove(nameof(Funcionario.SenhaHash));
            if (string.IsNullOrWhiteSpace(senha) || senha.Length < 6)
                ModelState.AddModelError("senha", "A senha deve ter ao menos 6 caracteres.");

            if (await _context.Hospedes.AnyAsync(h => h.Login == funcionario.Login))
                ModelState.AddModelError(nameof(Funcionario.Login), "Este login já está em uso por um hóspede.");

            if (ModelState.IsValid)
            {
                funcionario.SenhaHash = _hasher.HashPassword(funcionario, senha);
                _context.Add(funcionario);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Funcionário cadastrado com sucesso.";
                return RedirectToAction(nameof(Index));
            }
            CarregarListaHospedes(funcionario.HospedeId);
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

        // PT: Lista para o dropdown opcional "Hóspede vinculado" nas telas de funcionário.
        // EN: List for the optional "Linked guest" dropdown on employee screens.
        private void CarregarListaHospedes(int? selecionado = null)
        {
            ViewData["HospedeId"] = new SelectList(
                _context.Hospedes.OrderBy(h => h.Nome), "HospedeId", "Nome", selecionado);
        }
    }
}
