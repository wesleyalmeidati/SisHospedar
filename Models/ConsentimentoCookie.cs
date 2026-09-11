using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    // PT: Prova de que a escolha sobre cookies de medição foi feita, e qual foi. A LGPD
    //     exige que o controlador consiga demonstrar o consentimento (Art. 8º, §2º), então
    //     guardar só o cookie no navegador não basta: o titular pode limpar o navegador e
    //     não restaria registro nenhum do lado de cá.
    //     Deliberadamente NÃO guarda IP nem user-agent. Seria a tentação óbvia ("para
    //     identificar melhor"), mas IP é dado pessoal — a tabela que existe para provar
    //     respeito à privacidade viraria ela mesma um cadastro de rastreamento.
    //     Também não tem vínculo com HospedeId: a escolha é do navegador, não da conta, e
    //     ligá-las permitiria montar histórico de navegação por pessoa identificada.
    // EN: Proof that the choice about measurement cookies was made, and what it was. LGPD
    //     requires the controller to be able to demonstrate consent (Art. 8, §2), so keeping
    //     only the browser cookie isn't enough: the data subject can clear their browser and
    //     no record would remain on this side.
    //     It deliberately does NOT store IP or user-agent. That would be the obvious
    //     temptation ("to identify better"), but an IP is personal data — the very table
    //     that exists to prove privacy compliance would itself become a tracking record.
    //     It also has no link to HospedeId: the choice belongs to the browser, not the
    //     account, and linking them would allow building browsing history per identified
    //     person.
    public class ConsentimentoCookie
    {
        public int ConsentimentoCookieId { get; set; }

        // PT: Valor aleatório gerado no servidor, que NÃO é enviado ao navegador — de
        //     propósito. Devolvê-lo ao visitante tornaria cada registro religável a um
        //     navegador específico, ou seja, transformaria a prova de conformidade num
        //     identificador de rastreamento. Serve apenas como etiqueta interna de cada
        //     decisão; o navegador guarda só a escolha ("aceito"/"recusado"), que é tudo de
        //     que o servidor precisa para decidir se pode gravar o cookie de medição.
        // EN: A server-generated random value that is NOT sent to the browser — deliberately.
        //     Returning it to the visitor would make each record re-linkable to a specific
        //     browser, i.e. it would turn the compliance proof into a tracking identifier.
        //     It serves only as an internal label for each decision; the browser stores just
        //     the choice ("aceito"/"recusado"), which is all the server needs to decide
        //     whether it may write the measurement cookie.
        [Required]
        [StringLength(40)]
        public string IdentificadorAnonimo { get; set; } = string.Empty;

        public bool Aceito { get; set; }

        // PT: Consentimento vale para o texto vigente quando foi dado. Se a política mudar
        //     de forma relevante, comparar esta versão com a atual é o que permite saber
        //     quem precisa ser perguntado de novo.
        // EN: Consent applies to the text in force when it was given. If the policy changes
        //     materially, comparing this version against the current one is what tells you
        //     who needs to be asked again.
        [Required]
        [StringLength(20)]
        public string VersaoPolitica { get; set; } = string.Empty;

        public DateTime DataRegistro { get; set; }
    }
}
