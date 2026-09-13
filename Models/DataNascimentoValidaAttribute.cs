using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    // PT: Impede data de nascimento no futuro — que é o furo real: antes disto o
    //     cadastro aceitava 2050, e até "amanhã", sem reclamar. O limite inferior é só
    //     uma âncora de sanidade contra dedo trocado no ano (1750 também passava).
    //
    //     Deliberadamente NÃO existe idade mínima. Hotel recebe família, e criança é
    //     hóspede como qualquer outro — exigir 18 anos aqui quebraria o cadastro de um
    //     filho junto com os pais, que é caso comum e legítimo.
    //
    //     Os 130 anos não são arbitrários: PrivacidadeService usa 01/01/1900 como
    //     marcador ao anonimizar um hóspede (ver ExcluirContaAsync). Com um limite de
    //     120 anos, editar pela recepção uma ficha já anonimizada passaria a falhar a
    //     validação sem motivo. É o mesmo tipo de armadilha que os CPFs do seed tiveram
    //     quando o CpfValido entrou.
    // EN: Blocks a future date of birth — the actual hole: before this, registration
    //     accepted 2050, and even "tomorrow", without complaint. The lower bound is just
    //     a sanity anchor against a mistyped year (1750 got through too).
    //
    //     There is deliberately NO minimum age. Hotels host families, and a child is a
    //     guest like any other — requiring 18 here would break registering a child
    //     alongside their parents, which is a common and legitimate case.
    //
    //     The 130 years are not arbitrary: PrivacidadeService uses 1900-01-01 as the
    //     marker when anonymizing a guest (see ExcluirContaAsync). With a 120-year limit,
    //     editing an already-anonymized record from reception would start failing
    //     validation for no reason. It's the same kind of trap the seeded CPFs hit when
    //     CpfValido was introduced.
    public class DataNascimentoValidaAttribute : ValidationAttribute
    {
        private const int AnosMaximos = 130;

        protected override ValidationResult? IsValid(object? value, ValidationContext context)
        {
            // PT: [Required] separado cobre o campo vazio; aqui só validamos o valor.
            // EN: A separate [Required] covers the empty field; here we only validate the value.
            if (value is not DateTime data) return ValidationResult.Success;

            var hoje = DateTime.Today;

            if (data.Date > hoje)
                return new ValidationResult("A data de nascimento não pode ser no futuro.");

            if (data.Date < hoje.AddYears(-AnosMaximos))
                return new ValidationResult("Confira a data de nascimento: o ano informado parece incorreto.");

            return ValidationResult.Success;
        }
    }
}
