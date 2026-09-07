using Microsoft.AspNetCore.Http;

namespace SisHospedar.Services
{
    // PT: Quem foi autenticado — funcionário ou hóspede — pra o controller decidir
    //     pra onde redirecionar depois do login.
    // EN: Who got authenticated — employee or guest — so the controller can decide
    //     where to redirect after login.
    public enum TipoIdentidadeLogada { Funcionario, Hospede }

    // PT: Resultado da tentativa de login — ContaBloqueada é um caso à parte de
    //     CredenciaisInvalidas pra HomeController/ClienteController mostrarem uma
    //     mensagem específica (o ASP.NET Core Identity bloqueia a conta depois de
    //     várias tentativas erradas seguidas — ver AutenticacaoService).
    // EN: Login attempt outcome — ContaBloqueada is kept apart from
    //     CredenciaisInvalidas so HomeController/ClienteController can show a specific
    //     message (ASP.NET Core Identity locks the account out after several wrong
    //     attempts in a row — see AutenticacaoService).
    public enum ResultadoLogin { Sucesso, CredenciaisInvalidas, ContaBloqueada }

    // PT: Login único que descobre sozinho se as credenciais são de um funcionário ou
    //     de um hóspede e assina o cookie do esquema certo — não existe mais uma tela
    //     "de funcionário" e outra "de hóspede" com comportamentos diferentes: as duas
    //     rotas (/Home/Login e /Cliente/Login, mantidas por compatibilidade com
    //     [RequerFuncionario]/[RequerHospede] — que redirecionam pra cada uma delas —
    //     e com os testes de integração) chamam este mesmo serviço e se comportam de
    //     forma idêntica, não importa por qual delas a pessoa chegou. A verificação de
    //     senha, o hash e o controle de tentativas/bloqueio são feitos pelo
    //     UserManager/SignInManager do ASP.NET Core Identity (ver EntityUserStore) —
    //     a assinatura do cookie em si continua manual, com HttpContext.SignInAsync.
    // EN: A single login that figures out on its own whether the credentials belong to
    //     an employee or a guest and signs into the right cookie scheme — there's no
    //     more a separate "employee" screen and "guest" screen with different
    //     behavior: both routes (/Home/Login and /Cliente/Login, kept for
    //     compatibility with [RequerFuncionario]/[RequerHospede] — which redirect to
    //     each of them — and with the integration tests) call this same service and
    //     behave identically no matter which one the person landed on. Password
    //     verification, hashing, and attempt/lockout tracking are handled by ASP.NET
    //     Core Identity's UserManager/SignInManager (see EntityUserStore) — the actual
    //     cookie sign-in stays manual, via HttpContext.SignInAsync.
    public interface IAutenticacaoService
    {
        Task<(ResultadoLogin Resultado, TipoIdentidadeLogada? Tipo)> AutenticarAsync(HttpContext http, string login, string senha);
    }
}
