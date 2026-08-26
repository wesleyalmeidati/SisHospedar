using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    public enum CargoFuncionario
    {
        Recepcionista,
        Gerente,
        Limpeza,
        Manutencao,
        Vallet,
        Administrador
    }

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
    }
}
