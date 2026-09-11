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
    }
}
