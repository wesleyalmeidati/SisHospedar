using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    // PT: Cargo do funcionário — define o que ele vê no painel e quais ações pode executar
    //     (ver VerificarPermissao() nos controllers).
    // EN: Employee role — defines what they see on the dashboard and which actions
    //     they're allowed to perform (see VerificarPermissao() in the controllers).
    public enum CargoFuncionario
    {
        Recepcionista,
        Gerente,
        Limpeza,
        Manutencao,
        Vallet,
        Administrador
    }

    // PT: Representa um membro da equipe do hotel, com login e senha (hash) próprios,
    //     separados da conta de hóspede. Implementa IContaAutenticavel pra que
    //     EntityUserStore<Funcionario> (usado pelo UserManager/SignInManager do
    //     ASP.NET Core Identity) leia/grave login, hash, bloqueio e stamp de segurança
    //     direto nessas colunas — ver Authentication/EntityUserStore.cs.
    // EN: Represents a hotel staff member, with their own login and password (hash),
    //     separate from the guest account. Implements IContaAutenticavel so
    //     EntityUserStore<Funcionario> (used by ASP.NET Core Identity's
    //     UserManager/SignInManager) reads/writes login, hash, lockout, and security
    //     stamp straight from these columns — see Authentication/EntityUserStore.cs.
    public class Funcionario : IContaAutenticavel
    {
        public int FuncionarioId { get; set; }

        // PT: IContaAutenticavel.Id aponta pra FuncionarioId em vez de exigir uma
        //     propriedade "Id" própria — evita renomear a chave primária que o resto
        //     do projeto já usa.
        // EN: IContaAutenticavel.Id points at FuncionarioId instead of requiring its
        //     own "Id" property — avoids renaming the primary key the rest of the
        //     project already uses.
        int IContaAutenticavel.Id => FuncionarioId;

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(150)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Cargo")]
        public CargoFuncionario Cargo { get; set; }

        [Required(ErrorMessage = "O login é obrigatório.")]
        [StringLength(50)]
        [Display(Name = "Login")]
        public string Login { get; set; } = string.Empty;

        // PT: Opcional — usado só para enviar o link de redefinição de senha
        //     (ver ContaController/IEmailSender). Sem e-mail cadastrado, o
        //     funcionário precisa pedir pra um Administrador/Gerente redefinir
        //     a senha por ele (não há outro caminho de autoatendimento).
        // EN: Optional — used only to send the password-reset link (see
        //     ContaController/IEmailSender). With no email on file, the
        //     employee needs an Administrador/Gerente to reset their password
        //     for them (there's no other self-service path).
        [EmailAddress]
        [StringLength(150)]
        [Display(Name = "E-mail")]
        public string? Email { get; set; }

        // PT: Implementação explícita só pra bater a nulidade exigida pela interface
        //     (Hospede pode ter Login/SenhaHash nulos, Funcionario nunca tem) — o
        //     resto do projeto continua usando as propriedades públicas acima,
        //     sempre não-anuláveis.
        // EN: Explicit implementation just to match the nullability the interface
        //     requires (Hospede can have a null Login/SenhaHash, Funcionario never
        //     does) — the rest of the project keeps using the public properties
        //     above, always non-nullable.
        string? IContaAutenticavel.Login
        {
            get => Login;
            set => Login = value ?? string.Empty;
        }

        // Em produção: nunca armazenar senha em texto puro.
        // Este campo guarda apenas o HASH da senha (gerado com ASP.NET Core Identity
        // ou PasswordHasher<T>), nunca a senha original.
        [Required]
        [Display(Name = "Senha (hash)")]
        public string SenhaHash { get; set; } = string.Empty;

        string? IContaAutenticavel.SenhaHash
        {
            get => SenhaHash;
            set => SenhaHash = value ?? string.Empty;
        }

        // PT: Vínculo opcional para quando o funcionário também possui (ou possuiu) um
        //     cadastro de hóspede. Não unifica as identidades — cada uma mantém seu
        //     próprio login e senha; isso só serve para rastrear a relação entre as duas.
        // EN: Optional link for when the employee also has (or had) a guest account.
        //     Doesn't unify the identities — each keeps its own login and password;
        //     this only tracks the relationship between the two.
        [Display(Name = "Hóspede vinculado")]
        public int? HospedeId { get; set; }
        public Hospede? Hospede { get; set; }

        // PT: Campos exigidos por IUserSecurityStampStore/IUserLockoutStore
        //     (EntityUserStore) — nunca preenchidos por formulário, só pelo
        //     UserManager/SignInManager do Identity.
        // EN: Fields required by IUserSecurityStampStore/IUserLockoutStore
        //     (EntityUserStore) — never filled by a form, only by Identity's
        //     UserManager/SignInManager.
        public string? SecurityStamp { get; set; }
        public int AccessFailedCount { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public bool LockoutEnabled { get; set; } = true;
    }
}
