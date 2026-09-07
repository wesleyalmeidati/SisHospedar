using Microsoft.AspNetCore.Authorization;
using SisHospedar.Authentication;
using SisHospedar.Models;

namespace SisHospedar.Filters
{
    // PT: Atalho para [Authorize(AuthenticationSchemes = Esquemas.Funcionario, Roles = "...")]
    //     — além do cookie de funcionário válido, exige que a claim de Role (preenchida
    //     com o Cargo no login, ver HomeController.Login) seja uma das informadas.
    //     Sem sessão -> redireciona (LoginPath). Logado mas cargo errado -> 403
    //     (ver Events.OnRedirectToAccessDenied em Program.cs).
    // EN: Shortcut for [Authorize(AuthenticationSchemes = Esquemas.Funcionario, Roles = "...")]
    //     — besides a valid employee cookie, requires the Role claim (set to the Cargo
    //     at login, see HomeController.Login) to be one of those given. No session ->
    //     redirects (LoginPath). Logged in with the wrong role -> 403
    //     (see Events.OnRedirectToAccessDenied in Program.cs).
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequerCargoAttribute : AuthorizeAttribute
    {
        public RequerCargoAttribute(params CargoFuncionario[] cargos)
        {
            AuthenticationSchemes = Esquemas.Funcionario;
            Roles = string.Join(",", cargos);
        }
    }
}
