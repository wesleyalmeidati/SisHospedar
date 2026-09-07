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
        Manutencao,

        // PT: Tem uma Reserva Pendente em cima — some das listas de "disponível para
        //     reservar/check-in" (ver ReservaService), mas ainda não é uma Hospedagem
        //     de verdade. Volta a Disponivel se a reserva for cancelada, ou vira
        //     Ocupado quando a recepção confirma (check-in).
        // EN: Has a Pendente Reserva on it — drops out of "available to book/check-in"
        //     lists (see ReservaService), but isn't an actual Hospedagem yet. Goes back
        //     to Disponivel if the reservation is cancelled, or becomes Ocupado when
        //     reception confirms it (check-in).
        Reservado
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
