using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Authentication;
using SisHospedar.Data;
using SisHospedar.Filters;
using SisHospedar.Models;

namespace SisHospedar.Controllers
{
    // PT: Portal de autoatendimento do hóspede — cookie de autenticação próprio
    //     (esquema Esquemas.Hospede), independente do cookie de funcionário.
    // EN: Guest self-service portal — its own authentication cookie
    //     (Esquemas.Hospede scheme), independent from the employee cookie.
    public class ClienteController : Controller
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<Hospede> _hasher = new();

        public ClienteController(AppDbContext context) => _context = context;

        public IActionResult Registrar() => View();

        // PT: Cria a conta do hóspede (senha em hash) e já assina o cookie dele,
        //     encaminhando direto para criar a primeira reserva.
        // EN: Creates the guest account (hashed password) and immediately signs in
        //     their cookie, sending them straight to create their first booking.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(Hospede hospede, string senha)
        {
            ModelState.Remove(nameof(Hospede.SenhaHash));

            if (string.IsNullOrWhiteSpace(senha) || senha.Length < 6)
                ModelState.AddModelError("senha", "A senha deve ter ao menos 6 caracteres.");

            if (await _context.Hospedes.AnyAsync(h => h.Login == hospede.Login))
                ModelState.AddModelError("Login", "Este login já está em uso.");

            // PT: Login também não pode colidir com o de um funcionário — as duas
            //     identidades são independentes, mas compartilham o mesmo "namespace" de login.
            // EN: Login also can't collide with an employee's — the two identities are
            //     independent, but share the same login "namespace".
            if (await _context.Funcionarios.AnyAsync(f => f.Login == hospede.Login))
                ModelState.AddModelError("Login", "Este login já está em uso.");

            if (await _context.Hospedes.AnyAsync(h => h.Cpf == hospede.Cpf))
                ModelState.AddModelError("Cpf", "Já existe uma conta com este CPF.");

            if (ModelState.IsValid)
            {
                hospede.SenhaHash = _hasher.HashPassword(hospede, senha);
                _context.Add(hospede);
                await _context.SaveChangesAsync();

                await AssinarCookieAsync(hospede);

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
                await AssinarCookieAsync(hospede);
                return RedirectToAction(nameof(MinhaConta));
            }

            ModelState.AddModelError("", "Login ou senha inválidos.");
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(Esquemas.Hospede);
            return RedirectToAction("Index", "Home");
        }

        // PT: Área do hóspede — mostra a hospedagem ativa (com consumo) e as reservas
        //     pendentes de confirmação pela recepção.
        // EN: The guest's account area — shows the active stay (with consumption) and
        //     any bookings still pending reception's confirmation.
        [RequerHospede]
        public async Task<IActionResult> MinhaConta()
        {
            var hospedeId = ObterHospedeId();

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

        // PT: Permite ao hóspede estender a própria estadia, desde que ela pertença
        //     a ele, ainda esteja ativa e a nova data seja depois da entrada.
        // EN: Lets the guest extend their own stay, provided it belongs to them,
        //     is still active, and the new date is after check-in.
        [RequerHospede]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Prorrogar(int hospedagemId, DateTime novaDataSaida)
        {
            var hospedeId = ObterHospedeId();

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

        // PT: O hóspede só sinaliza o pedido (SolicitouEncerramento = true); quem de
        //     fato finaliza a hospedagem é a recepção, em HospedagemController.
        // EN: The guest only flags the request (SolicitouEncerramento = true); reception
        //     is the one who actually finalizes the stay, in HospedagemController.
        [RequerHospede]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarEncerramento(int hospedagemId)
        {
            var hospedeId = ObterHospedeId();

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

        private async Task AssinarCookieAsync(Hospede hospede)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, hospede.HospedeId.ToString()),
                new(ClaimTypes.Name, hospede.Nome)
            };
            var identidade = new ClaimsIdentity(claims, Esquemas.Hospede);
            await HttpContext.SignInAsync(Esquemas.Hospede, new ClaimsPrincipal(identidade));
        }

        // PT: [RequerHospede] já garante que a claim existe e é um int válido.
        // EN: [RequerHospede] already guarantees the claim exists and is a valid int.
        private int ObterHospedeId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
