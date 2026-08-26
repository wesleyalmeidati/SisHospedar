using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    public enum StatusReserva
    {
        Pendente,
        Confirmada,
        Cancelada
    }

    // Uma Reserva é o pedido do cliente feito pelo site (antes de chegar ao hotel).
    // Quando a recepção confirma, uma Hospedagem de verdade é criada (check-in).
    public class Reserva
    {
        public int ReservaId { get; set; }

        [Required]
        public int HospedeId { get; set; }
        public Hospede? Hospede { get; set; }

        [Required]
        public int ApartamentoId { get; set; }
        public Apartamento? Apartamento { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Data de Entrada")]
        public DateTime DataEntrada { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Data de Saída Prevista")]
        public DateTime DataSaidaPrevista { get; set; }

        [Required]
        public StatusReserva Status { get; set; } = StatusReserva.Pendente;

        public DateTime DataCriacao { get; set; } = DateTime.Now;
    }
}
