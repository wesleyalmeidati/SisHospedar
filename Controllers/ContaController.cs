using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Fluxo de "esqueci minha senha", unificado como o login — tenta o login
    //     digitado como funcionário e depois como hóspede, igual a
    //     AutenticacaoService.AutenticarAsync.
    //     MODO DEMONSTRAÇÃO: a tela de confirmação mostra o link de redefinição direto
    //     na página (com aviso), em vez de enviar e-mail de verdade — o projeto não
    //     tem provedor de e-mail configurado. O mecanismo real do Identity (geração e
    //     validação do token via UserManager, expiração, invalidação automática pelo
    //     SecurityStamp ao trocar a senha) é o mesmo que backearia um e-mail de
    //     verdade — só o canal de entrega está simplificado.
    // EN: "Forgot my password" flow, unified like login — tries the typed login as an
    //     employee, then as a guest, same as AutenticacaoService.AutenticarAsync.
    //     DEMO MODE: the confirmation screen shows the reset link directly on the page
    //     (with a warning), instead of sending a real email — the project has no email
    //     provider configured. Identity's real mechanics (token generation/validation
    //     via UserManager, expiry, automatic invalidation through SecurityStamp once
    //     the password changes) are the same ones that would back a real email — only
    //     the delivery channel is simplified.
    public class ContaController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Funcionario> _userManagerFuncionario;
        private readonly UserManager<Hospede> _userManagerHospede;

        public ContaController(
            AppDbContext context,
            UserManager<Funcionario> userManagerFuncionario,
            UserManager<Hospede> userManagerHospede)
        {
            _context = context;
            _userManagerFuncionario = userManagerFuncionario;
            _userManagerHospede = userManagerHospede;
        }

        public IActionResult EsqueciSenha() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EsqueciSenha(string login)
        {
            var funcionario = await _context.Funcionarios.FirstOrDefaultAsync(f => f.Login == login);
            if (funcionario != null)
            {
                var token = await _userManagerFuncionario.GeneratePasswordResetTokenAsync(funcionario);
                return MostrarLinkGerado("funcionario", funcionario.FuncionarioId, token);
            }

            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.Login == login);
            if (hospede != null && !string.IsNullOrEmpty(hospede.SenhaHash))
            {
                var token = await _userManagerHospede.GeneratePasswordResetTokenAsync(hospede);
                return MostrarLinkGerado("hospede", hospede.HospedeId, token);
            }

            ViewData["Link"] = null;
            return View("EsqueciSenhaConfirmacao");
        }

        private IActionResult MostrarLinkGerado(string tipo, int id, string token)
        {
            ViewData["Link"] = Url.Action(nameof(RedefinirSenha), "Conta", new { tipo, id, token }, Request.Scheme);
            return View("EsqueciSenhaConfirmacao");
        }

        public async Task<IActionResult> RedefinirSenha(string tipo, int id, string token)
        {
            if (!await ContaExisteAsync(tipo, id)) return NotFound();

            ViewData["Tipo"] = tipo;
            ViewData["Id"] = id;
            ViewData["Token"] = token;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RedefinirSenha(string tipo, int id, string token, string novaSenha)
        {
            var resultado = string.Equals(tipo, "funcionario", StringComparison.OrdinalIgnoreCase)
                ? await RedefinirAsync(_userManagerFuncionario, id, token, novaSenha)
                : await RedefinirAsync(_userManagerHospede, id, token, novaSenha);

            if (resultado == null) return NotFound();

            if (resultado.Succeeded)
            {
                TempData["Sucesso"] = "Senha redefinida com sucesso. Faça login com a nova senha.";
                return RedirectToAction("Login", "Home");
            }

            foreach (var erro in resultado.Errors)
                ModelState.AddModelError("", erro.Description);

            ViewData["Tipo"] = tipo;
            ViewData["Id"] = id;
            ViewData["Token"] = token;
            return View();
        }

        private static async Task<IdentityResult?> RedefinirAsync<TEntity>(
            UserManager<TEntity> userManager, int id, string token, string novaSenha)
            where TEntity : class
        {
            var conta = await userManager.FindByIdAsync(id.ToString());
            return conta == null ? null : await userManager.ResetPasswordAsync(conta, token, novaSenha);
        }

        private async Task<bool> ContaExisteAsync(string tipo, int id) =>
            string.Equals(tipo, "funcionario", StringComparison.OrdinalIgnoreCase)
                ? await _userManagerFuncionario.FindByIdAsync(id.ToString()) != null
                : await _userManagerHospede.FindByIdAsync(id.ToString()) != null;
    }
}
