using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Authentication;
using SisHospedar.Data;
using SisHospedar.Services;

namespace SisHospedar.Controllers
{
    // PT: Página pública do hotel e um dos dois pontos de entrada do login único
    //     (o outro é ClienteController.Login) — ver IAutenticacaoService.
    // EN: The hotel's public landing page and one of the two entry points for the
    //     unified login (the other is ClienteController.Login) — see
    //     IAutenticacaoService.
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IAutenticacaoService _autenticacao;

        public HomeController(AppDbContext context, IAutenticacaoService autenticacao)
        {
            _context = context;
            _autenticacao = autenticacao;
        }

        // PT: Página pública do hotel — mostra os 3 tipos de acomodação (Solteiro/
        //     Casal/Suíte Master) com sua diária, não os apartamentos individuais.
        //     O tipo de cada apartamento é determinado pela Capacidade (1/2/3) —
        //     não há uma entidade "TipoAcomodacao" separada no modelo. Mostramos os
        //     3 tipos sempre (mesmo com todos os quartos daquele tipo ocupados);
        //     a disponibilidade real por data é checada na hora de reservar.
        // EN: Public hotel page — shows the 3 accommodation types (Single/Double/
        //     Master Suite) with their rate, not individual apartments. Each
        //     apartment's type is determined by its Capacidade (1/2/3) — there's no
        //     separate "TipoAcomodacao" entity in the model. All 3 types always show
        //     (even if every room of that type is occupied); real per-date
        //     availability is checked at booking time.
        public async Task<IActionResult> Index()
        {
            var tipos = (await _context.Apartamentos
                .GroupBy(a => a.Capacidade)
                .Select(g => new { Capacidade = g.Key, ValorDiaria = g.Min(a => a.ValorDiaria) })
                .OrderBy(t => t.ValorDiaria)
                .ToListAsync())
                .Select(t => (t.Capacidade, t.ValorDiaria))
                .ToList();
            return View(tipos);
        }

        public IActionResult Login() => View();

        // PT: Login único — não sabe (nem precisa saber) se quem está digitando é
        //     funcionário ou hóspede. IAutenticacaoService tenta as duas identidades
        //     e diz qual bateu; o redirecionamento depois é só consequência disso.
        // EN: Unified login — it doesn't know (or need to know) whether the person
        //     typing is an employee or a guest. IAutenticacaoService tries both
        //     identities and reports which one matched; the redirect afterward is
        //     just a consequence of that.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string login, string senha)
        {
            var (resultado, tipo) = await _autenticacao.AutenticarAsync(HttpContext, login, senha);

            if (tipo == TipoIdentidadeLogada.Funcionario) return RedirectToAction("Index", "Painel");
            if (tipo == TipoIdentidadeLogada.Hospede) return RedirectToAction("MinhaConta", "Cliente");

            ModelState.AddModelError("", resultado == ResultadoLogin.ContaBloqueada
                ? "Conta temporariamente bloqueada após várias tentativas inválidas. Tente novamente mais tarde ou redefina sua senha."
                : "Login ou senha inválidos.");
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(Esquemas.Funcionario);
            return RedirectToAction(nameof(Index));
        }
    }
}
