namespace SisHospedar.Models
{
    // PT: Implementada por Funcionario e Hospede pra que EntityUserStore<T> (ver
    //     Authentication/EntityUserStore.cs) consiga ler/gravar login, hash de senha,
    //     bloqueio e stamp de segurança sem herdar de IdentityUser<T> — evita duplicar
    //     as tabelas de apoio do Identity (UserClaims/UserRoles/UserTokens) por
    //     identidade e mantém FuncionarioId/HospedeId/Login/SenhaHash exatamente como
    //     já eram (nenhuma renomeação).
    // EN: Implemented by Funcionario and Hospede so EntityUserStore<T> (see
    //     Authentication/EntityUserStore.cs) can read/write login, password hash,
    //     lockout, and security stamp without inheriting from IdentityUser<T> — avoids
    //     duplicating Identity's support tables (UserClaims/UserRoles/UserTokens) per
    //     identity and keeps FuncionarioId/HospedeId/Login/SenhaHash exactly as they
    //     were (no renaming).
    public interface IContaAutenticavel
    {
        int Id { get; }
        string? Login { get; set; }
        string? SenhaHash { get; set; }
        string? SecurityStamp { get; set; }
        int AccessFailedCount { get; set; }
        DateTimeOffset? LockoutEnd { get; set; }
        bool LockoutEnabled { get; set; }
    }
}
