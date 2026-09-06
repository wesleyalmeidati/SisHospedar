using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SisHospedar.Models
{
    // PT: Representa uma estadia real (check-in feito) de um Hospede em um Apartamento.
    //     Criada ao confirmar uma Reserva ou diretamente no balcão pela recepção.
    // EN: Represents an actual stay (checked in) of a Hospede in an Apartamento.
    //     Created when a Reserva is confirmed, or directly at the front desk.
    public class Hospedagem
    {
        public int HospedagemId { get; set; }

        [Required]
        [Display(Name = "Hóspede")]
        public int HospedeId { get; set; }
        public Hospede? Hospede { get; set; }

        [Required]
        [Display(Name = "Apartamento")]
        public int ApartamentoId { get; set; }
        public Apartamento? Apartamento { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Data de Entrada")]
        public DateTime DataEntrada { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "Data de Saída")]
        public DateTime? DataSaida { get; set; }

        [Display(Name = "Finalizada")]
        public bool Finalizada { get; set; } = false;

        // Marcado quando o próprio cliente pede o encerramento pela "Minha Conta".
        // A recepção vê esse pedido e confirma a saída.
        [Display(Name = "Encerramento Solicitado")]
        public bool SolicitouEncerramento { get; set; } = false;

        public ICollection<Consumo> Consumos { get; set; } = new List<Consumo>();

        [NotMapped]
        [Display(Name = "Total de Consumo (R$)")]
        public decimal TotalConsumo => Consumos?.Sum(c => c.ValorTotal) ?? 0;
    }
}
