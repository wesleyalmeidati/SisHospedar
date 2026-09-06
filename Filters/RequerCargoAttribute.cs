using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SisHospedar.Models;

namespace SisHospedar.Filters
{
    // PT: Além de exigir sessão de funcionário (herda RequerFuncionario), exige que o
    //     Cargo esteja entre os informados; caso contrário, nega com 403. Substitui o
    //     VerificarPermissao() que existia no FuncionarioController.
    // EN: In addition to requiring an employee session (inherits RequerFuncionario),
    //     requires the Cargo to be one of those given; otherwise denies with 403.
    //     Replaces the VerificarPermissao() that used to live in FuncionarioController.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequerCargoAttribute : RequerFuncionarioAttribute
    {
        private readonly CargoFuncionario[] _cargos;

        public RequerCargoAttribute(params CargoFuncionario[] cargos) => _cargos = cargos;

        public override void OnAuthorization(AuthorizationFilterContext context)
        {
            base.OnAuthorization(context);
            if (context.Result != null) return; // já negado por falta de sessão

            var cargo = context.HttpContext.Session.GetString("FuncionarioCargo");
            if (!_cargos.Any(c => c.ToString() == cargo))
            {
                context.Result = new ObjectResult(
                    $"Acesso negado: requer um dos cargos a seguir: {string.Join(", ", _cargos)}.")
                {
                    StatusCode = 403
                };
            }
        }
    }
}
