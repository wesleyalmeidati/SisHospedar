using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    public class Produto
    {
        public int ProdutoId { get; set; }

        [Required(ErrorMessage = "O nome do produto é obrigatório.")]
        [StringLength(150)]
        [Display(Name = "Nome")]
        public string Nome { get; set; } = string.Empty;

        [Required]
        [Range(0, 100000)]
        [Display(Name = "Preço (R$)")]
        [DataType(DataType.Currency)]
        public decimal Preco { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        [Display(Name = "Estoque")]
        public int Estoque { get; set; }

        public ICollection<Consumo> Consumos { get; set; } = new List<Consumo>();
    }
}
