using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    // PT: Contagem AGREGADA de acessos, uma linha por caminho por dia — não uma linha por
    //     visita. Duas razões, nesta ordem de importância:
    //     1) Privacidade: uma linha por visita, mesmo sem nome, acumula um rastro temporal
    //        que pode reidentificar alguém por cruzamento. Um contador diário não guarda
    //        "quem fez o quê e quando", só "quantas vezes esta página foi aberta hoje".
    //        Por não conter dado pessoal, essa contagem não depende de consentimento.
    //     2) Custo: o Azure SQL do projeto é tier Basic (5 DTU). A tabela cresce com
    //        (páginas × dias), não com o tráfego, então continua pequena para sempre.
    // EN: AGGREGATE hit count, one row per path per day — not one row per visit. Two
    //     reasons, in this order of importance:
    //     1) Privacy: one row per visit, even without a name, builds a temporal trail that
    //        can re-identify someone by correlation. A daily counter stores no "who did
    //        what and when", only "how many times this page was opened today". Holding no
    //        personal data, this count needs no consent.
    //     2) Cost: the project's Azure SQL is Basic tier (5 DTU). The table grows with
    //        (pages × days), not with traffic, so it stays small forever.
    public class VisitaPagina
    {
        public int VisitaPaginaId { get; set; }

        [Required]
        [StringLength(200)]
        public string Caminho { get; set; } = string.Empty;

        public DateTime Data { get; set; }

        public int Total { get; set; }

        // PT: Só aumenta quando o visitante consentiu com o cookie de medição E ele indica
        //     passagem anterior. É exatamente aqui que o consentimento faz diferença
        //     observável: sem ele, este número simplesmente não cresce. É o que impede o
        //     banner de ser enfeite — aceitar e recusar produzem resultados distintos.
        // EN: Only grows when the visitor consented to the measurement cookie AND it shows
        //     a previous visit. This is precisely where consent makes an observable
        //     difference: without it, this number simply doesn't grow. It's what keeps the
        //     banner from being decoration — accepting and refusing produce different
        //     outcomes.
        public int TotalRecorrentes { get; set; }
    }
}
