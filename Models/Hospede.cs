using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        // PT: Marcador gravado no CPF quando o titular pede exclusão e há histórico a
        //     preservar (ver PrivacidadeService.ExcluirContaAsync). Vive aqui, e não solto
        //     como string no service, porque duas partes do sistema precisam concordar sobre
        //     ele: quem anonimiza e quem detecta que já está anonimizado.
        // EN: Marker written into the CPF when the data subject requests erasure and there is
        //     history to preserve (see PrivacidadeService.ExcluirContaAsync). It lives here
        //     rather than as a loose string in the service because two parts of the system
        //     have to agree on it: the one that anonymizes and the one that detects an
        //     already-anonymized record.
        public const string PrefixoCpfAnonimizado = "ANON";

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

        // PT: Uma ficha anonimizada não pode ser editada pela recepção, e antes disto o
        //     sistema falhava nisso de um jeito confuso: o CPF vira um marcador que não passa
        //     no CpfValido, então abrir a ficha e salvar qualquer coisa devolvia "CPF
        //     inválido" num campo que ninguém tinha tocado, sem saída nenhuma.
        //     Recusar explicitamente é melhor por dois motivos: a mensagem diz a verdade, e
        //     reeditar uma ficha anonimizada seria repersonalizar um dado que o titular pediu
        //     para remover — o contrário do que a anonimização existe para fazer.
        // EN: An anonymized record must not be edited by reception, and before this the system
        //     failed at that confusingly: the CPF becomes a marker that doesn't pass
        //     CpfValido, so opening the record and saving anything returned "CPF inválido" on
        //     a field nobody had touched, with no way out.
        //     Refusing explicitly is better for two reasons: the message tells the truth, and
        //     re-editing an anonymized record would re-personalize data the subject asked to
        //     have removed — the opposite of what anonymization exists to do.
        [NotMapped]
        public bool EstaAnonimizado => Cpf.StartsWith(PrefixoCpfAnonimizado, StringComparison.Ordinal);

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

