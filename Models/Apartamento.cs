using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    // PT: Fonte única de verdade sobre a disponibilidade de um apartamento para
    //     reserva/hospedagem — não existe cálculo de disponibilidade separado.
    // EN: Single source of truth for a room's booking eligibility — there's no
    //     separate availability-calculation logic elsewhere.
    public enum StatusApartamento
    {
        Disponivel,
        Ocupado,
        Manutencao
    }

    // PT: Representa um quarto/apartamento do hotel.
    // EN: Represents a hotel room/apartment.
    public class Apartamento
    {
        public int ApartamentoId { get; set; }

        [Required(ErrorMessage = "O número do apartamento é obrigatório.")]
        [Display(Name = "Número")]
        public string Numero { get; set; } = string.Empty;

        [Required]
        [Range(1, 20)]
        [Display(Name = "Capacidade (hóspedes)")]
        public int Capacidade { get; set; }

        [Required]
        [Range(0, 100000)]
        [Display(Name = "Valor da Diária (R$)")]
        [DataType(DataType.Currency)]
        public decimal ValorDiaria { get; set; }

        [Required]
        [Display(Name = "Status")]
        public StatusApartamento Status { get; set; } = StatusApartamento.Disponivel;

        public ICollection<Hospedagem> Hospedagens { get; set; } = new List<Hospedagem>();
    }
}
