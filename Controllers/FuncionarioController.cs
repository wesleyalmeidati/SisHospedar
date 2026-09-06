using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Gerencia a equipe do hotel. Restrito a Administrador/Gerente.
    // EN: Manages hotel staff. Restricted to Administrador/Gerente roles.
    [RequerCargo(CargoFuncionario.Administrador, CargoFuncionario.Gerente)]
    public class FuncionarioController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<Funcionario> _hasher = new();

        public FuncionarioController(AppDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            return View(await _context.Funcionarios.OrderBy(f => f.Nome).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var funcionario = await _context.Funcionarios
                .Include(f => f.Hospede)
                .FirstOrDefaultAsync(f => f.FuncionarioId == id);
            if (funcionario == null) return NotFound();
            return View(funcionario);
        }

        public IActionResult Create()
        {
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
            if (id == null) return NotFound();
            var funcionario = await _context.Funcionarios.FirstOrDefaultAsync(f => f.FuncionarioId == id);
            if (funcionario == null) return NotFound();
            return View(funcionario);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
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
