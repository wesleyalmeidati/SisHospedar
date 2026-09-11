using Microsoft.AspNetCore.Mvc;
using SisHospedar.Services;

namespace SisHospedar.Controllers
{
    // PT: Aviso de privacidade público (LGPD Art. 9º — o titular tem direito a saber, de
    //     forma clara, o que é tratado e com que finalidade). Fica fora de qualquer filtro
    //     de autorização de propósito: precisa ser legível por quem ainda não criou conta,
    //     inclusive antes de decidir criar.
    // EN: Public privacy notice (LGPD Art. 9 — the data subject has the right to clearly
    //     know what is processed and why). It deliberately sits outside any authorization
    //     filter: it has to be readable by someone who hasn't created an account yet,
    //     including before deciding to create one.
    public class PrivacidadeController : Controller
    {
        private readonly IAnalyticsService _analytics;

        public PrivacidadeController(IAnalyticsService analytics) => _analytics = analytics;

        public IActionResult Index() => View();

        // PT: Política de Cookies detalhada, separada do aviso geral porque cumpre uma
        //     função diferente: o aviso explica o tratamento de dados, esta página lista
        //     tecnicamente cada cookie gravado. Também é o destino do link na barra de
        //     aviso, então precisa ser pública.
        // EN: Detailed Cookie Policy, kept separate from the general notice because it
        //     serves a different purpose: the notice explains data processing, this page
        //     technically lists every cookie written. It's also the target of the notice
        //     bar's link, so it has to be public.
        public IActionResult Cookies() => View();

        // PT: Tela de preferências permanente. A LGPD trata o consentimento como revogável a
        //     qualquer tempo (Art. 8º, §5º), e "revogável" só é verdade se existir um lugar
        //     estável para revogar — banner que some depois do primeiro clique não cumpre isso.
        // EN: Permanent preferences screen. LGPD treats consent as revocable at any time
        //     (Art. 8, §5), and "revocable" is only true if there's a stable place to revoke
        //     it — a banner that disappears after the first click doesn't satisfy that.
        public IActionResult Preferencias()
        {
            ViewData["Decisao"] = Request.Cookies[Medicao.CookieConsentimento];
            return View();
        }

        // PT: Único ponto que grava a decisão, usado tanto pela barra quanto pela tela de
        //     preferências. É POST com antiforgery porque altera estado; e é formulário
        //     comum, não fetch, para continuar funcionando sem JavaScript.
        // EN: The single place that records the decision, used both by the bar and by the
        //     preferences screen. It's a POST with antiforgery because it changes state; and
        //     it's a plain form, not fetch, so it keeps working without JavaScript.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Consentimento(bool aceito, string? returnUrl)
        {
            await _analytics.RegistrarConsentimentoAsync(Guid.NewGuid().ToString("N"), aceito);

            // PT: A recusa também é gravada no navegador. Se só o aceite fosse lembrado, quem
            //     recusa veria a barra de novo a cada página — insistir até a pessoa ceder é
            //     justamente o padrão escuro que a ANPD condena.
            // EN: The refusal is stored in the browser too. If only acceptance were
            //     remembered, whoever refuses would see the bar again on every page —
            //     nagging until the person gives in is exactly the dark pattern the ANPD
            //     objects to.
            Response.Cookies.Append(
                Medicao.CookieConsentimento,
                aceito ? "aceito" : "recusado",
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = Request.IsHttps,
                    Expires = DateTimeOffset.UtcNow.AddMonths(12)
                });

            // PT: Revogar tem que apagar de verdade o que já foi gravado, não só impedir
            //     gravações futuras — senão o identificador continuaria no navegador depois
            //     de a pessoa ter dito não.
            // EN: Revoking has to actually delete what was already written, not merely
            //     prevent future writes — otherwise the identifier would stay in the browser
            //     after the person said no.
            if (!aceito) Response.Cookies.Delete(Medicao.CookieVisitante);

            // PT: Só aceita retorno para caminho local — returnUrl vindo do formulário é
            //     entrada do usuário, e redirecionar para host externo seria open redirect.
            // EN: Only local paths are accepted — returnUrl comes from the form, i.e. user
            //     input, and redirecting to an external host would be an open redirect.
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Preferencias));
        }
    }
}
