using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    // PT: Representa um hóspede do hotel. Pode ser cadastrado pela recepção (sem login)
    //     ou criar sua própria conta pelo portal do cliente (com Login/SenhaHash).
    //     Implementa IContaAutenticavel pra que EntityUserStore<Hospede> (usado pelo
    //     UserManager/SignInManager do ASP.NET Core Identity) leia/grave login, hash,
    //     bloqueio e stamp de segurança direto nessas colunas — ver
    //     Authentication/EntityUserStore.cs.
    // EN: Represents a hotel guest. Can be registered by reception (no login) or
    //     create their own account through the guest portal (with Login/SenhaHash).
    //     Implements IContaAutenticavel so EntityUserStore<Hospede> (used by ASP.NET
    //     Core Identity's UserManager/SignInManager) reads/writes login, hash,
    //     lockout, and security stamp straight from these columns — see
    //     Authentication/EntityUserStore.cs.
    public class Hospede : IContaAutenticavel
    {
        public int HospedeId { get; set; }

        // PT: Ver o mesmo comentário em Funcionario.cs — evita renomear HospedeId.
        // EN: See the same comment in Funcionario.cs — avoids renaming HospedeId.
        int IContaAutenticavel.Id => HospedeId;

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(150)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF é obrigatório.")]
        [StringLength(14)]
        [CpfValido]
        [Display(Name = "CPF")]
        public string Cpf { get; set; } = string.Empty;

        [Required(ErrorMessage = "O telefone é obrigatório.")]
        [StringLength(20)]
        [Display(Name = "Telefone")]
        public string Telefone { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Data de Nascimento")]
        public DateTime DataNascimento { get; set; }

        // Campos abaixo só são preenchidos quando o hóspede cria uma conta
        // própria no site (reserva online). Hóspedes cadastrados manualmente
        // pela recepção (check-in de balcão) podem não ter login.
        [EmailAddress]
        [StringLength(150)]
        [Display(Name = "E-mail")]
        public string? Email { get; set; }

        [StringLength(50)]
        [Display(Name = "Login")]
        public string? Login { get; set; }

        [Display(Name = "Senha (hash)")]
        public string? SenhaHash { get; set; }

        public ICollection<Hospedagem> Hospedagens { get; set; } = new List<Hospedagem>();
        public ICollection<Reserva> Reservas { get; set; } = new List<Reserva>();

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

