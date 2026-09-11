using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IWebHostEnvironment _ambiente;
        private readonly IMemoryCache _cache;

        public HomeController(
            AppDbContext context,
            IAutenticacaoService autenticacao,
            IWebHostEnvironment ambiente,
            IMemoryCache cache)
        {
            _context = context;
            _autenticacao = autenticacao;
            _ambiente = ambiente;
            _cache = cache;
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
        // PT: Esta consulta é a única coisa entre a home pública e o banco, e o Azure SQL
        //     do projeto é tier Basic — cada visita pagava esse ida-e-volta. O resultado
        //     (faixa de preço por capacidade) muda só quando alguém cadastra apartamento
        //     ou mexe na diária, o que é raro, então 5 minutos de cache em memória é
        //     bastante margem: uma alteração aparece na home em no máximo 5 minutos e
        //     nenhuma outra tela usa este cache. Em memória do próprio processo, sem
        //     serviço externo e sem custo.
        // EN: This query is the only thing between the public home page and the database,
        //     and the project's Azure SQL is on the Basic tier — every visit was paying
        //     that round-trip. The result (price range per capacity) only changes when
        //     someone adds an apartment or edits a rate, which is rare, so a 5-minute
        //     in-memory cache is plenty of headroom: a change shows up on the home page
        //     within 5 minutes at worst, and no other screen uses this cache. In-process
        //     memory, no external service, no cost.
        private const string ChaveCacheTipos = "home:tipos-acomodacao";

        public async Task<IActionResult> Index()
        {
            var tipos = await _cache.GetOrCreateAsync(ChaveCacheTipos, async entrada =>
            {
                entrada.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return (await _context.Apartamentos
                    .AsNoTracking()
                    .GroupBy(a => a.Capacidade)
                    .Select(g => new { Capacidade = g.Key, ValorDiaria = g.Min(a => a.ValorDiaria) })
                    .OrderBy(t => t.ValorDiaria)
                    .ToListAsync())
                    .Select(t => (t.Capacidade, t.ValorDiaria))
                    .ToList();
            }) ?? [];

            // PT: wwwroot/videos/ é gitignored, então o hero.mp4 existe só na máquina
            //     de quem o baixou — nunca na imagem publicada. Sem esta checagem a
            //     tag <video autoplay> pede o arquivo em toda visita à home e leva 404,
            //     desperdiçando uma requisição; o fundo correto em produção é a foto
            //     definida no site.css, que já é o fallback natural sem a tag.
            // EN: wwwroot/videos/ is gitignored, so hero.mp4 only exists on the machine
            //     of whoever downloaded it — never in the published image. Without this
            //     check the <video autoplay> tag requests the file on every home page
            //     visit and gets a 404, wasting a request; the correct production
            //     background is the photo set in site.css, which is the natural
            //     fallback when the tag isn't rendered.
            ViewData["TemVideoHero"] = _ambiente.WebRootFileProvider
                .GetFileInfo("videos/hero.mp4").Exists;

            return View(tipos);
        }

        // PT: Destino do app.UseExceptionHandler("/Home/Error") em produção. Sem esta
        //     action a rota não existia e uma exceção não tratada virava um 404 confuso.
        //     A View não mostra detalhe nenhum da exceção de propósito — stack trace em
        //     produção é vazamento de informação.
        // EN: Target of app.UseExceptionHandler("/Home/Error") in production. Without
        //     this action the route didn't exist and an unhandled exception turned into
        //     a confusing 404. The View deliberately shows no exception detail — a
        //     stack trace in production is an information leak.
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Error() => View();

        // PT: As duas portas do login único precisam tratar returnUrl igual — o handler de
        //     cookie anexa esse parâmetro ao redirecionar quem não está autenticado, e até
        //     aqui ninguém o lia: a pessoa entrava e caía no painel, perdendo a página que
        //     tentava acessar. Ver o mesmo tratamento em ClienteController.Login.
        // EN: Both doors of the unified login must handle returnUrl the same way — the
        //     cookie handler appends that parameter when redirecting an unauthenticated
        //     visitor, and until now nobody read it: the person signed in and landed on the
        //     dashboard, losing the page they were trying to reach. See the matching
        //     handling in ClienteController.Login.
        public IActionResult Login(string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // PT: Login único — não sabe (nem precisa saber) se quem está digitando é
        //     funcionário ou hóspede. IAutenticacaoService tenta as duas identidades
        //     e diz qual bateu; o redirecionamento depois é só consequência disso.
        // EN: Unified login — it doesn't know (or need to know) whether the person
        //     typing is an employee or a guest. IAutenticacaoService tries both
        //     identities and reports which one matched; the redirect afterward is
        //     just a consequence of that.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("AutenticacaoPublica")]
        public async Task<IActionResult> Login(string login, string senha, string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;
            var (resultado, tipo) = await _autenticacao.AutenticarAsync(HttpContext, login, senha);

            if (tipo == TipoIdentidadeLogada.Funcionario) return RedirectToAction("Index", "Painel");

            if (tipo == TipoIdentidadeLogada.Hospede)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("MinhaConta", "Cliente");
            }

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
