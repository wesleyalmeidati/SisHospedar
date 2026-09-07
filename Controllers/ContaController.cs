using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;
using SisHospedar.Services;

namespace SisHospedar.Controllers
{
    // PT: Fluxo de "esqueci minha senha", unificado como o login — tenta o login
    //     digitado como funcionário e depois como hóspede, igual a
    //     AutenticacaoService.AutenticarAsync. O link de redefinição vai por
    //     e-mail de verdade (IEmailSender/SmtpEmailSender), nunca aparece na tela.
    //     A confirmação mostra sempre a mesma mensagem genérica, exista ou não a
    //     conta e tenha ou não e-mail cadastrado — evita revelar se um login existe.
    // EN: "Forgot my password" flow, unified like login — tries the typed login as an
    //     employee, then as a guest, same as AutenticacaoService.AutenticarAsync. The
    //     reset link goes out by real email (IEmailSender/SmtpEmailSender), never
    //     shown on screen. The confirmation always shows the same generic message,
    //     whether or not the account exists or has an email on file — avoids
    //     revealing whether a given login exists.
    public class ContaController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Funcionario> _userManagerFuncionario;
        private readonly UserManager<Hospede> _userManagerHospede;
        private readonly IEmailSender _emailSender;

        public ContaController(
            AppDbContext context,
            UserManager<Funcionario> userManagerFuncionario,
            UserManager<Hospede> userManagerHospede,
            IEmailSender emailSender)
        {
            _context = context;
            _userManagerFuncionario = userManagerFuncionario;
            _userManagerHospede = userManagerHospede;
            _emailSender = emailSender;
        }

        public IActionResult EsqueciSenha() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EsqueciSenha(string login)
        {
            var funcionario = await _context.Funcionarios.FirstOrDefaultAsync(f => f.Login == login);
            if (funcionario != null && !string.IsNullOrEmpty(funcionario.Email))
            {
                var token = await _userManagerFuncionario.GeneratePasswordResetTokenAsync(funcionario);
                await EnviarLinkAsync(funcionario.Email, "funcionario", funcionario.FuncionarioId, token);
                return View("EsqueciSenhaConfirmacao");
            }

            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.Login == login);
            if (hospede != null && !string.IsNullOrEmpty(hospede.SenhaHash) && !string.IsNullOrEmpty(hospede.Email))
            {
                var token = await _userManagerHospede.GeneratePasswordResetTokenAsync(hospede);
                await EnviarLinkAsync(hospede.Email, "hospede", hospede.HospedeId, token);
                return View("EsqueciSenhaConfirmacao");
            }

            // PT: Mesma tela, mesmo status — tanto faz se o login não existe, se
            //     existe mas não tem e-mail cadastrado, ou se o e-mail acabou de ser
            //     enviado. Ver comentário da classe.
            // EN: Same screen, same status — doesn't matter whether the login
            //     doesn't exist, exists with no email on file, or the email was just
            //     sent. See the class-level comment.
            return View("EsqueciSenhaConfirmacao");
        }

        private async Task EnviarLinkAsync(string destinatario, string tipo, int id, string token)
        {
            var link = Url.Action(nameof(RedefinirSenha), "Conta", new { tipo, id, token }, Request.Scheme)!;
            var corpo = $"""
                <p>Você pediu a redefinição da sua senha no SisHospedar.</p>
                <p><a href="{link}">Clique aqui para escolher uma nova senha</a></p>
                <p>Se você não pediu isso, pode ignorar este e-mail.</p>
                """;
            await _emailSender.EnviarAsync(destinatario, "Redefinição de senha - SisHospedar", corpo);
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
