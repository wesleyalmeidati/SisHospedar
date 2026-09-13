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

        // PT: As mensagens estão explícitas em todos os atributos, inclusive nos que
        //     antes não tinham. Sem ErrorMessage, o ASP.NET Core gera a mensagem padrão
        //     dele — em inglês. O formulário ficava misturando idiomas para o visitante:
        //     "O nome é obrigatório." logo ao lado de "The field Nome must be a string
        //     with a maximum length of 150."
        // EN: Messages are spelled out on every attribute, including those that had none.
        //     Without ErrorMessage, ASP.NET Core generates its own default — in English.
        //     The form was mixing languages for the visitor: "O nome é obrigatório."
        //     right next to "The field Nome must be a string with a maximum length of 150."
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(150, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF é obrigatório.")]
        [StringLength(14, ErrorMessage = "O CPF deve ter no máximo {1} caracteres.")]
        [CpfValido]
        [Display(Name = "CPF")]
        public string Cpf { get; set; } = string.Empty;

        [Required(ErrorMessage = "O telefone é obrigatório.")]
        [StringLength(20, ErrorMessage = "O telefone deve ter no máximo {1} caracteres.")]
        [TelefoneValido]
        [Display(Name = "Telefone")]
        public string Telefone { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data de nascimento é obrigatória.")]
        [DataType(DataType.Date)]
        [DataNascimentoValida]
        [Display(Name = "Data de Nascimento")]
        public DateTime DataNascimento { get; set; }

        // Campos abaixo só são preenchidos quando o hóspede cria uma conta
        // própria no site (reserva online). Hóspedes cadastrados manualmente
        // pela recepção (check-in de balcão) podem não ter login.
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(150, ErrorMessage = "O e-mail deve ter no máximo {1} caracteres.")]
        [Display(Name = "E-mail")]
        public string? Email { get; set; }

        [StringLength(50, ErrorMessage = "O login deve ter no máximo {1} caracteres.")]
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

