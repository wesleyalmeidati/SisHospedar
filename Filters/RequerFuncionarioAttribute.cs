using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SisHospedar.Filters
{
    // PT: Exige uma sessão de funcionário ativa (Session["FuncionarioId"]); sem ela,
    //     redireciona para Home/Login. Substitui o padrão repetido de VerificarSessao()
    //     que existia em cada controller — agora é uma anotação em vez de código colado.
    // EN: Requires an active employee session (Session["FuncionarioId"]); redirects to
    //     Home/Login if absent. Replaces the repeated VerificarSessao() pattern that
    //     used to live in every controller — now it's an annotation, not pasted code.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequerFuncionarioAttribute : Attribute, IAuthorizationFilter
    {
        public virtual void OnAuthorization(AuthorizationFilterContext context)
        {
            if (context.HttpContext.Session.GetInt32("FuncionarioId") == null)
            {
                context.Result = new RedirectToActionResult("Login", "Home", null);
            }
        }
    }
}
