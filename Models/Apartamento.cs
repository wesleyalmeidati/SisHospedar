using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // PT: Nomenclatura de acomodação seguindo a prática do setor hoteleiro, que separa
        //     três coisas frequentemente confundidas: CATEGORIA (padrão de conforto:
        //     Standard, Executivo, Master), OCUPAÇÃO/CAMA (solteiro, casal, triplo) e
        //     diferenciais. A recomendação corrente é compor o nome nessa ordem —
        //     "categoria + ocupação" —, que é como sai "Standard Solteiro" ou
        //     "Executivo Casal". As siglas internacionais equivalentes são SGL, DBL e TPL.
        //
        //     Mora aqui, e não na View, porque esses nomes já existiam espalhados em três
        //     lugares: o dicionário de Views/Home/Index.cshtml, os comentários do seed em
        //     Program.cs e nada no modelo. Três cópias da mesma regra divergem na primeira
        //     alteração — alguém renomeia "Executivo" na home e o dropdown continua
        //     dizendo outra coisa, sem nada quebrar.
        //
        //     São [NotMapped]: derivam de Capacidade, então não viram coluna nem migration.
        //     E são static para a home poder usá-las, já que lá o modelo é uma tupla
        //     (Capacidade, ValorDiaria), não a entidade.
        // EN: Accommodation naming following hotel-industry practice, which separates three
        //     frequently conflated things: CATEGORY (comfort tier: Standard, Executive,
        //     Master), OCCUPANCY/BED (single, double, triple) and differentiators. Current
        //     guidance is to compose the name in that order — "category + occupancy" —
        //     which is how "Standard Solteiro" or "Executivo Casal" come about. The
        //     equivalent international codes are SGL, DBL and TPL.
        //
        //     It lives here rather than in the View because these names already existed
        //     scattered across three places: the dictionary in Views/Home/Index.cshtml, the
        //     seed comments in Program.cs, and nothing on the model. Three copies of the
        //     same rule diverge on the first edit — someone renames "Executivo" on the home
        //     page and the dropdown keeps saying something else, with nothing breaking.
        //
        //     They're [NotMapped]: derived from Capacidade, so no column and no migration.
        //     And they're static so the home page can use them too, since its model there is
        //     a (Capacidade, ValorDiaria) tuple rather than the entity.
        public static string CategoriaPara(int capacidade) => capacidade switch
        {
            1 => "Standard",
            2 => "Executivo",
            3 => "Master",
            _ => "Suíte"
        };

        public static string AcomodacaoPara(int capacidade) => capacidade switch
        {
            1 => "Solteiro",
            2 => "Casal",
            3 => "Triplo",
            _ => $"até {capacidade} pessoas"
        };

        [NotMapped]
        public string Categoria => CategoriaPara(Capacidade);

        [NotMapped]
        public string Acomodacao => AcomodacaoPara(Capacidade);

        // PT: Rótulo dos <select> de apartamento. Antes mostravam só o número, o que obriga
        //     quem reserva a adivinhar se o "03" cabe duas pessoas.
        // EN: Label for the apartment <select> controls. They used to show only the number,
        //     which forces whoever is booking to guess whether "03" fits two people.
        [NotMapped]
        public string DescricaoParaSelecao => $"{Numero} — {Categoria} {Acomodacao}";
    }
}
