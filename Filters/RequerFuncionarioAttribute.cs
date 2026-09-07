using Microsoft.AspNetCore.Authorization;
using SisHospedar.Authentication;

namespace SisHospedar.Filters
{
    // PT: Atalho para [Authorize(AuthenticationSchemes = Esquemas.Funcionario)] — exige
    //     um cookie de funcionário válido; sem ele, o próprio esquema de cookie
    //     redireciona para Home/Login (LoginPath configurado em Program.cs).
    // EN: Shortcut for [Authorize(AuthenticationSchemes = Esquemas.Funcionario)] —
    //     requires a valid employee cookie; without one, the cookie scheme itself
    //     redirects to Home/Login (LoginPath configured in Program.cs).
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequerFuncionarioAttribute : AuthorizeAttribute
    {
        public RequerFuncionarioAttribute() => AuthenticationSchemes = Esquemas.Funcionario;
    }
}
