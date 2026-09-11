using Microsoft.AspNetCore.Mvc;

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
    }
}
