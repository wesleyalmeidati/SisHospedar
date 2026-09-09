using System.ComponentModel.DataAnnotations;

namespace SisHospedar.Models
{
    // PT: Valida CPF de verdade — não só "tem 11 dígitos", mas os dois dígitos
    //     verificadores batem com o algoritmo oficial (módulo 11 sobre os 9
    //     primeiros dígitos, depois sobre os 10). Aceita o valor formatado
    //     ("123.456.789-09") ou só números, já que os dois formatos aparecem no
    //     projeto (formulário digita com máscara visual, mas o binding do MVC não
    //     aplica máscara nenhuma). Sequências com todos os dígitos iguais
    //     (111.111.111-11 etc.) satisfazem a fórmula matematicamente mas nunca são
    //     CPFs emitidos de verdade — Receita Federal e qualquer validador de mercado
    //     rejeitam essas explicitamente, e aqui fazemos o mesmo.
    // EN: Validates a real CPF — not just "has 11 digits", but that the two check
    //     digits actually match the official algorithm (modulo 11 over the first 9
    //     digits, then over 10). Accepts either the formatted value
    //     ("123.456.789-09") or digits only, since both show up in this project (the
    //     form's input has a visual mask, but MVC model binding doesn't apply any
    //     mask itself). All-same-digit sequences (111.111.111-11 etc.) satisfy the
    //     formula mathematically but are never real issued CPFs — the Federal
    //     Revenue and every real-world validator explicitly reject those, and so do we.
    public class CpfValidoAttribute : ValidationAttribute
    {
        public CpfValidoAttribute() : base("CPF inválido.") { }

        protected override ValidationResult? IsValid(object? value, ValidationContext context)
        {
            // PT: [Required] separado já cobre "vazio" — aqui só validamos formato/
            //     dígito verificador quando algo foi digitado.
            // EN: A separate [Required] already covers "empty" — here we only
            //     validate format/check digit when something was actually typed.
            if (value is not string texto || string.IsNullOrWhiteSpace(texto))
                return ValidationResult.Success;

            var digitos = new string(texto.Where(char.IsDigit).ToArray());

            if (digitos.Length != 11 || digitos.Distinct().Count() == 1)
                return new ValidationResult(ErrorMessage);

            var numeros = digitos.Select(c => c - '0').ToArray();

            int CalcularDigito(int tamanho)
            {
                var soma = 0;
                for (var i = 0; i < tamanho; i++)
                    soma += numeros[i] * (tamanho + 1 - i);
                var resto = soma % 11;
                return resto < 2 ? 0 : 11 - resto;
            }

            var digito1 = CalcularDigito(9);
            var digito2 = CalcularDigito(10);

            return numeros[9] == digito1 && numeros[10] == digito2
                ? ValidationResult.Success
                : new ValidationResult(ErrorMessage);
        }
    }
}
