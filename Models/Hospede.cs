using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    public class Hospede
    {
        public int HospedeId { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(150)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF é obrigatório.")]
        [StringLength(14)]
        [Display(Name = "CPF")]
        public string Cpf { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "Telefone")]
        public string? Telefone { get; set; }

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
    }
}

