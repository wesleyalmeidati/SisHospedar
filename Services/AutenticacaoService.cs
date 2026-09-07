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
        private readonly PasswordHasher<Funcionario> _hasherFuncionario = new();
        private readonly PasswordHasher<Hospede> _hasherHospede = new();

        public AutenticacaoService(AppDbContext context) => _context = context;

        // PT: Tenta como funcionário primeiro, depois como hóspede — as duas tabelas
        //     têm login próprio (unique index em cada uma) e Registrar já impede um
        //     hóspede novo de escolher um login que colida com o de um funcionário,
        //     então na prática não há ambiguidade real entre as duas tentativas.
        // EN: Tries as an employee first, then as a guest — both tables have their
        //     own login (a unique index on each), and Registrar already blocks a new
        //     guest from picking a login that collides with an employee's, so in
        //     practice there's no real ambiguity between the two attempts.
        public async Task<TipoIdentidadeLogada?> AutenticarAsync(HttpContext http, string login, string senha)
        {
            var funcionario = await _context.Funcionarios.FirstOrDefaultAsync(f => f.Login == login);
            if (funcionario != null &&
                _hasherFuncionario.VerifyHashedPassword(funcionario, funcionario.SenhaHash, senha) == PasswordVerificationResult.Success)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, funcionario.FuncionarioId.ToString()),
                    new(ClaimTypes.Name, funcionario.Nome),
                    new(ClaimTypes.Role, funcionario.Cargo.ToString())
                };
                await http.SignInAsync(Esquemas.Funcionario, new ClaimsPrincipal(new ClaimsIdentity(claims, Esquemas.Funcionario)));
                return TipoIdentidadeLogada.Funcionario;
            }

            var hospede = await _context.Hospedes.FirstOrDefaultAsync(h => h.Login == login);
            if (hospede != null && !string.IsNullOrEmpty(hospede.SenhaHash) &&
                _hasherHospede.VerifyHashedPassword(hospede, hospede.SenhaHash, senha) == PasswordVerificationResult.Success)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, hospede.HospedeId.ToString()),
                    new(ClaimTypes.Name, hospede.Nome)
                };
                await http.SignInAsync(Esquemas.Hospede, new ClaimsPrincipal(new ClaimsIdentity(claims, Esquemas.Hospede)));
                return TipoIdentidadeLogada.Hospede;
            }

            return null;
        }
    }
}
