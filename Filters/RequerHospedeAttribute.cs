using Microsoft.AspNetCore.Authorization;
using SisHospedar.Authentication;

namespace SisHospedar.Filters
{
    // PT: Atalho para [Authorize(AuthenticationSchemes = Esquemas.Hospede)] — exige um
    //     cookie de hóspede válido; sem ele, redireciona para Cliente/Login (LoginPath
    //     configurado em Program.cs).
    // EN: Shortcut for [Authorize(AuthenticationSchemes = Esquemas.Hospede)] — requires
    //     a valid guest cookie; without one, redirects to Cliente/Login (LoginPath
    //     configured in Program.cs).
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequerHospedeAttribute : AuthorizeAttribute
    {
        public RequerHospedeAttribute() => AuthenticationSchemes = Esquemas.Hospede;
    }
}
