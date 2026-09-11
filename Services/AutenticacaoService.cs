using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Authentication;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Services
{
    public class AutenticacaoService : IAutenticacaoService
    {
        private readonly AppDbContext _context;
        private readonly SignInManager<Funcionario> _signInManagerFuncionario;
        private readonly SignInManager<Hospede> _signInManagerHospede;
        private readonly ILogger<AutenticacaoService> _logger;

        public AutenticacaoService(
            AppDbContext context,
            SignInManager<Funcionario> signInManagerFuncionario,
            SignInManager<Hospede> signInManagerHospede,
            ILogger<AutenticacaoService> logger)
        {
            _context = context;
            _signInManagerFuncionario = signInManagerFuncionario;
            _signInManagerHospede = signInManagerHospede;
            _logger = logger;
        }

        // PT: Tenta como funcionário primeiro, depois como hóspede — as duas tabelas
        //     têm login próprio (unique index em cada uma) e Registrar já impede um
        //     hóspede novo de escolher um login que colida com o de um funcionário,
        //     então na prática não há ambiguidade real entre as duas tentativas.
        //     CheckPasswordSignInAsync(..., lockoutOnFailure: true) é quem verifica o
        //     hash, incrementa AccessFailedCount a cada erro e bloqueia a conta ao
        //     atingir o limite — tudo via EntityUserStore, sem hash/contagem manual.
        // EN: Tries as an employee first, then as a guest — both tables have their
        //     own login (a unique index on each), and Registrar already blocks a new
        //     guest from picking a login that collides with an employee's, so in
        //     practice there's no real ambiguity between the two attempts.
        //     CheckPasswordSignInAsync(..., lockoutOnFailure: true) is what verifies
        //     the hash, increments AccessFailedCount on each miss, and locks the
        //     account out once the threshold is hit — all through EntityUserStore, no
        //     manual hashing/counting.
        public async Task<(ResultadoLogin Resultado, TipoIdentidadeLogada? Tipo)> AutenticarAsync(HttpContext http, string login, string senha)
        {
            var funcionario = await _context.Funcionarios.FirstOrDefaultAsync(f => f.Login == login);
            if (funcionario != null)
            {
                var resultado = await _signInManagerFuncionario.CheckPasswordSignInAsync(funcionario, senha, lockoutOnFailure: true);
                if (resultado.Succeeded)
                {
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, funcionario.FuncionarioId.ToString()),
                        new(ClaimTypes.Name, funcionario.Nome),
                        new(ClaimTypes.Role, funcionario.Cargo.ToString())
                    };
                    await http.SignInAsync(Esquemas.Funcionario, new ClaimsPrincipal(new ClaimsIdentity(claims, Esquemas.Funcionario)));
                    return (ResultadoLogin.Sucesso, TipoIdentidadeLogada.Funcionario);
                }
                // PT: Só o bloqueio é registrado, e só ele carrega o login. Tentativa
                //     inválida isolada não vira log de propósito: alguém que digita a senha
                //     no campo de usuário por engano colocaria a própria senha no log, que
                //     tem retenção longa. O bloqueio é raro, é acionável (indica ataque ou
                //     usuário travado) e precisa dizer qual conta foi afetada.
                // EN: Only lockout is logged, and only it carries the login. A single failed
                //     attempt is deliberately not logged: someone who types their password
                //     into the username field would put their own password into a
                //     long-retained log. Lockout is rare, actionable (it signals an attack or
                //     a stuck user) and has to say which account was affected.
                if (resultado.IsLockedOut)
                {
                    _logger.LogWarning("Conta de funcionário bloqueada por tentativas inválidas: {Login}.", login);
                    return (ResultadoLogin.ContaBloqueada, null);
                }
            }

            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.Login == login);
            if (hospede != null && !string.IsNullOrEmpty(hospede.SenhaHash))
            {
                var resultado = await _signInManagerHospede.CheckPasswordSignInAsync(hospede, senha, lockoutOnFailure: true);
                if (resultado.Succeeded)
                {
                    var claims = new List<Claim>
                    {
                        new(ClaimTypes.NameIdentifier, hospede.HospedeId.ToString()),
                        new(ClaimTypes.Name, hospede.Nome)
                    };
                    await http.SignInAsync(Esquemas.Hospede, new ClaimsPrincipal(new ClaimsIdentity(claims, Esquemas.Hospede)));
                    return (ResultadoLogin.Sucesso, TipoIdentidadeLogada.Hospede);
                }
                if (resultado.IsLockedOut)
                {
                    _logger.LogWarning("Conta de hóspede bloqueada por tentativas inválidas: {Login}.", login);
                    return (ResultadoLogin.ContaBloqueada, null);
                }
            }

            return (ResultadoLogin.CredenciaisInvalidas, null);
        }
    }
}
