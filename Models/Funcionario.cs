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
    //     separados da conta de hóspede.
    // EN: Represents a hotel staff member, with their own login and password (hash),
    //     separate from the guest account.
    public class Funcionario
    {
        public int FuncionarioId { get; set; }

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

        // Em produção: nunca armazenar senha em texto puro.
        // Este campo guarda apenas o HASH da senha (gerado com ASP.NET Core Identity
        // ou PasswordHasher<T>), nunca a senha original.
        [Required]
        [Display(Name = "Senha (hash)")]
        public string SenhaHash { get; set; } = string.Empty;

        // PT: Vínculo opcional para quando o funcionário também possui (ou possuiu) um
        //     cadastro de hóspede. Não unifica as identidades — cada uma mantém seu
        //     próprio login e senha; isso só serve para rastrear a relação entre as duas.
        // EN: Optional link for when the employee also has (or had) a guest account.
        //     Doesn't unify the identities — each keeps its own login and password;
        //     this only tracks the relationship between the two.
        [Display(Name = "Hóspede vinculado")]
        public int? HospedeId { get; set; }
        public Hospede? Hospede { get; set; }
    }
}
