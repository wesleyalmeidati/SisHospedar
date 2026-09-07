using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Authentication
{
    // PT: Store customizada do ASP.NET Core Identity que ensina o UserManager/
    //     SignInManager a ler/gravar direto nas colunas que Funcionario e Hospede já
    //     tinham (Login, SenhaHash, FuncionarioId/HospedeId) — em vez de herdar de
    //     IdentityUser<T> (que traria colunas redundantes e exigiria renomear a chave
    //     primária e o login pelo projeto todo) ou usar o UserStore pronto do
    //     Microsoft.AspNetCore.Identity.EntityFrameworkCore (que espera tabelas de
    //     apoio como UserClaims/UserRoles/UserTokens, desnecessárias aqui — as claims
    //     de login são montadas na hora do sign-in em AutenticacaoService, não pelo
    //     Identity). Uma única classe genérica serve tanto Funcionario quanto Hospede.
    // EN: Custom ASP.NET Core Identity store that teaches UserManager/SignInManager to
    //     read/write straight into the columns Funcionario and Hospede already had
    //     (Login, SenhaHash, FuncionarioId/HospedeId) — instead of inheriting from
    //     IdentityUser<T> (which would bring redundant columns and require renaming
    //     the primary key and the login across the project) or using the ready-made
    //     store from Microsoft.AspNetCore.Identity.EntityFrameworkCore (which expects
    //     support tables like UserClaims/UserRoles/UserTokens, unneeded here — login
    //     claims are built at sign-in time in AutenticacaoService, not by Identity).
    //     One generic class serves both Funcionario and Hospede.
    public class EntityUserStore<TEntity> :
        IUserStore<TEntity>,
        IUserPasswordStore<TEntity>,
        IUserLockoutStore<TEntity>,
        IUserSecurityStampStore<TEntity>
        where TEntity : class, IContaAutenticavel
    {
        private readonly AppDbContext _context;
        public EntityUserStore(AppDbContext context) => _context = context;

        // ---- IUserStore ----

        public Task<string> GetUserIdAsync(TEntity user, CancellationToken ct) =>
            Task.FromResult(user.Id.ToString());

        public Task<string?> GetUserNameAsync(TEntity user, CancellationToken ct) =>
            Task.FromResult(user.Login);

        public Task SetUserNameAsync(TEntity user, string? userName, CancellationToken ct)
        {
            user.Login = userName;
            return Task.CompletedTask;
        }

        // PT: Não existe coluna de "login normalizado" — a normalização (maiúsculas)
        //     acontece só em memória, na hora de comparar em FindByNameAsync.
        // EN: There's no separate "normalized login" column — normalization
        //     (uppercasing) happens only in memory, at FindByNameAsync comparison time.
        public Task<string?> GetNormalizedUserNameAsync(TEntity user, CancellationToken ct) =>
            Task.FromResult(user.Login?.ToUpperInvariant());

        public Task SetNormalizedUserNameAsync(TEntity user, string? normalizedName, CancellationToken ct) =>
            Task.CompletedTask;

        public async Task<IdentityResult> CreateAsync(TEntity user, CancellationToken ct)
        {
            _context.Add(user);
            await _context.SaveChangesAsync(ct);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateAsync(TEntity user, CancellationToken ct)
        {
            _context.Update(user);
            await _context.SaveChangesAsync(ct);
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> DeleteAsync(TEntity user, CancellationToken ct)
        {
            _context.Remove(user);
            await _context.SaveChangesAsync(ct);
            return IdentityResult.Success;
        }

        // PT: DbSet<TEntity>.FindAsync busca por valor de chave primária, não por uma
        //     expressão LINQ — funciona independente do nome da propriedade de PK
        //     (FuncionarioId/HospedeId), então não esbarra em limitação nenhuma de
        //     tradução de LINQ sobre TEntity genérico/via interface.
        // EN: DbSet<TEntity>.FindAsync looks up by primary key value, not a LINQ
        //     expression — works regardless of the PK property's name
        //     (FuncionarioId/HospedeId), so it doesn't hit any LINQ-translation
        //     limitation around the generic/interface-typed TEntity.
        public async Task<TEntity?> FindByIdAsync(string userId, CancellationToken ct)
        {
            if (!int.TryParse(userId, out var id)) return null;
            return await _context.Set<TEntity>().FindAsync(new object[] { id }, ct);
        }

        // PT: EF.Property<T> busca a coluna pelo nome ("Login"), em vez de acessar
        //     user.Login via a interface dentro da expressão — evita depender de como
        //     o EF Core traduziria (ou não) uma chamada de membro de interface num
        //     Where() sobre um TEntity genérico.
        // EN: EF.Property<T> looks the column up by name ("Login"), instead of
        //     accessing user.Login through the interface inside the expression —
        //     avoids depending on whether EF Core would (or wouldn't) translate an
        //     interface member call inside a Where() over a generic TEntity.
        public Task<TEntity?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
            _context.Set<TEntity>().FirstOrDefaultAsync(u =>
                EF.Property<string?>(u, nameof(IContaAutenticavel.Login)) != null &&
                EF.Property<string?>(u, nameof(IContaAutenticavel.Login))!.ToUpper() == normalizedUserName,
                ct)!;

        // ---- IUserPasswordStore ----

        public Task SetPasswordHashAsync(TEntity user, string? passwordHash, CancellationToken ct)
        {
            user.SenhaHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task<string?> GetPasswordHashAsync(TEntity user, CancellationToken ct) =>
            Task.FromResult(user.SenhaHash);

        public Task<bool> HasPasswordAsync(TEntity user, CancellationToken ct) =>
            Task.FromResult(!string.IsNullOrEmpty(user.SenhaHash));

        // ---- IUserLockoutStore ----

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(TEntity user, CancellationToken ct) =>
            Task.FromResult(user.LockoutEnd);

        public Task SetLockoutEndDateAsync(TEntity user, DateTimeOffset? lockoutEnd, CancellationToken ct)
        {
            user.LockoutEnd = lockoutEnd;
            return Task.CompletedTask;
        }

        public Task<int> IncrementAccessFailedCountAsync(TEntity user, CancellationToken ct)
        {
            user.AccessFailedCount++;
            return Task.FromResult(user.AccessFailedCount);
        }

        public Task ResetAccessFailedCountAsync(TEntity user, CancellationToken ct)
        {
            user.AccessFailedCount = 0;
            return Task.CompletedTask;
        }

        public Task<int> GetAccessFailedCountAsync(TEntity user, CancellationToken ct) =>
            Task.FromResult(user.AccessFailedCount);

        public Task<bool> GetLockoutEnabledAsync(TEntity user, CancellationToken ct) =>
            Task.FromResult(user.LockoutEnabled);

        public Task SetLockoutEnabledAsync(TEntity user, bool enabled, CancellationToken ct)
        {
            user.LockoutEnabled = enabled;
            return Task.CompletedTask;
        }

        // ---- IUserSecurityStampStore ----

        public Task SetSecurityStampAsync(TEntity user, string stamp, CancellationToken ct)
        {
            user.SecurityStamp = stamp;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecurityStampAsync(TEntity user, CancellationToken ct) =>
            Task.FromResult(user.SecurityStamp);

        // PT: AppDbContext é scoped pelo DI e descartado pelo container — nada pra
        //     liberar aqui.
        // EN: AppDbContext is DI-scoped and disposed by the container — nothing to
        //     release here.
        public void Dispose() { }
    }
}
