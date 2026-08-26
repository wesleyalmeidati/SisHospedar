using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SisHospedar.Models
{
    public class Consumo
    {
        public int ConsumoId { get; set; }

        [Required]
        [Display(Name = "Hospedagem")]
        public int HospedagemId { get; set; }
        public Hospedagem? Hospedagem { get; set; }

        [Required]
        [Display(Name = "Produto")]
        public int ProdutoId { get; set; }
        public Produto? Produto { get; set; }

        [Required]
        [Range(1, 1000)]
        [Display(Name = "Quantidade")]
        public int Quantidade { get; set; } = 1;

        [Column(TypeName = "decimal(10,2)")]
        [Display(Name = "Valor Unitário (R$)")]
        public decimal ValorUnitario { get; set; }

        [NotMapped]
        [Display(Name = "Valor Total (R$)")]
        public decimal ValorTotal => Quantidade * ValorUnitario;
    }
}
