namespace SisHospedar.Services
{
    // PT: Nomes e constantes num lugar só — o middleware, o controller e as Views precisam
    //     concordar sobre eles, e string solta espalhada é como esse tipo de coisa passa a
    //     divergir sem ninguém notar.
    // EN: Names and constants in one place — the middleware, the controller and the Views
    //     all have to agree on them, and loose strings scattered around are how this kind of
    //     thing silently drifts apart.
    public static class Medicao
    {
        // PT: Guarda a DECISÃO (aceitou ou recusou). É estritamente necessário: o servidor
        //     precisa saber a escolha para decidir se pode gravar o cookie de medição, e
        //     localStorage não serve porque não é enviado ao servidor. Recusar também grava
        //     este cookie — é o que impede o banner de reaparecer a cada página.
        // EN: Stores the DECISION (accepted or refused). It is strictly necessary: the
        //     server needs to know the choice to decide whether it may write the measurement
        //     cookie, and localStorage won't do because it isn't sent to the server.
        //     Refusing also writes this cookie — that's what stops the banner from
        //     reappearing on every page.
        public const string CookieConsentimento = "SisHospedar.Consentimento";

        // PT: O cookie OPCIONAL. Só existe se houve aceite, e é apagado se a pessoa mudar de
        //     ideia. Carrega apenas um valor aleatório, sem nada derivado do visitante.
        // EN: The OPTIONAL cookie. It only exists after an acceptance, and is deleted if the
        //     person changes their mind. It carries only a random value, nothing derived
        //     from the visitor.
        public const string CookieVisitante = "SisHospedar.Visitante";

        // PT: Versão do texto da política. Gravada junto do consentimento para que, se a
        //     política mudar de forma relevante, dê para saber quem consentiu com a versão
        //     antiga e precisa ser perguntado de novo.
        // EN: Version of the policy text. Stored alongside the consent so that, if the policy
        //     changes materially, it's possible to tell who consented to the old version and
        //     needs to be asked again.
        public const string VersaoPolitica = "2026-09-11";
    }

    public record VisitaResumo(string Caminho, int Total, int Recorrentes);

    public interface IAnalyticsService
    {
        // PT: Recebe o acumulado, não uma visita por vez. É o que faz o buffer valer a pena:
        //     100 visitas na mesma página viram um UPDATE, não cem.
        // EN: Takes the accumulated totals, not one visit at a time. This is what makes the
        //     buffer worthwhile: 100 visits to the same page become one UPDATE, not a hundred.
        Task RegistrarVisitasAsync(string caminho, int total, int recorrentes);
        Task RegistrarConsentimentoAsync(string identificadorAnonimo, bool aceito);
        Task<IReadOnlyList<VisitaResumo>> ResumoAsync(int dias);
        Task<int> TotalNoPeriodoAsync(int dias);
    }
}
