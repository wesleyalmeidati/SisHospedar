using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    // PT: Valida a SUBSTÂNCIA do telefone (quantos dígitos tem), não o formato. Antes
    //     disto o campo aceitava qualquer texto — "nao-e-telefone" entrava sem reclamar,
    //     e o hotel ficava com uma ficha sem meio de contato.
    //
    //     A escolha de não impor formato é deliberada. Um regex tipo "(99) 99999-9999"
    //     rejeitaria telefone fixo de oito dígitos, número escrito sem parênteses,
    //     número internacional de hóspede estrangeiro — todos legítimos. Validação de
    //     formato rígida demais é pior que validação nenhuma: ela recusa dado válido, e
    //     o usuário não tem como saber qual máscara o sistema quer.
    //
    //     A faixa vai de 10 (DDD + 8 dígitos, fixo brasileiro) a 15 dígitos, que é o
    //     máximo do padrão internacional E.164 e cobre hóspede de fora com código de país.
    // EN: Validates the phone's SUBSTANCE (how many digits it has), not its format.
    //     Before this the field accepted any text — "nao-e-telefone" got in without
    //     complaint, leaving the hotel with a record and no way to reach the guest.
    //
    //     Not enforcing a format is deliberate. A regex like "(99) 99999-9999" would
    //     reject an eight-digit landline, a number written without parentheses, an
    //     international number from a foreign guest — all legitimate. Overly rigid format
    //     validation is worse than none: it refuses valid data, and the user has no way
    //     to know which mask the system wants.
    //
    //     The range runs from 10 (area code + 8 digits, a Brazilian landline) to 15
    //     digits, the maximum in the international E.164 standard, which covers a guest
    //     from abroad with a country code.
    public class TelefoneValidoAttribute : ValidationAttribute
    {
        private const int MinimoDigitos = 10;
        private const int MaximoDigitos = 15;

        public TelefoneValidoAttribute()
            : base("Informe um telefone válido, com DDD.") { }

        protected override ValidationResult? IsValid(object? value, ValidationContext context)
        {
            // PT: [Required] separado cobre o campo vazio.
            // EN: A separate [Required] covers the empty field.
            if (value is not string texto || string.IsNullOrWhiteSpace(texto))
                return ValidationResult.Success;

            var digitos = texto.Count(char.IsDigit);

            return digitos >= MinimoDigitos && digitos <= MaximoDigitos
                ? ValidationResult.Success
                : new ValidationResult(ErrorMessage);
        }
    }
}
