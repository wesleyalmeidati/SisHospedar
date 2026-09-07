using Microsoft.AspNetCore.Http;

namespace SisHospedar.Services
{
    // PT: Quem foi autenticado — funcionário ou hóspede — pra o controller decidir
    //     pra onde redirecionar depois do login.
    // EN: Who got authenticated — employee or guest — so the controller can decide
    //     where to redirect after login.
    public enum TipoIdentidadeLogada { Funcionario, Hospede }

    // PT: Login único que descobre sozinho se as credenciais são de um funcionário ou
    //     de um hóspede e assina o cookie do esquema certo — não existe mais uma tela
    //     "de funcionário" e outra "de hóspede" com comportamentos diferentes: as duas
    //     rotas (/Home/Login e /Cliente/Login, mantidas por compatibilidade com
    //     [RequerFuncionario]/[RequerHospede] — que redirecionam pra cada uma delas —
    //     e com os testes de integração) chamam este mesmo serviço e se comportam de
    //     forma idêntica, não importa por qual delas a pessoa chegou.
    // EN: A single login that figures out on its own whether the credentials belong to
    //     an employee or a guest and signs into the right cookie scheme — there's no
    //     more a separate "employee" screen and "guest" screen with different
    //     behavior: both routes (/Home/Login and /Cliente/Login, kept for
    //     compatibility with [RequerFuncionario]/[RequerHospede] — which redirect to
    //     each of them — and with the integration tests) call this same service and
    //     behave identically no matter which one the person landed on.
    public interface IAutenticacaoService
    {
        Task<TipoIdentidadeLogada?> AutenticarAsync(HttpContext http, string login, string senha);
    }
}
