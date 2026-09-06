using SisHospedar.Models;

namespace SisHospedar.Services
{
    // PT: Resultado do check-out — separado de (bool,string) porque "não encontrada"
    //     deve virar NotFound() no controller, não um erro de validação re-exibido no form.
    // EN: Check-out result — kept apart from (bool,string) because "not found" should
    //     become a NotFound() in the controller, not a validation error shown on a form.
    public enum ResultadoCheckOut { Sucesso, NaoEncontrada }

    // PT: Regras de negócio do check-in/check-out — abrir uma hospedagem ocupa o
    //     apartamento, finalizar libera. Extraído do controller pra ser testável
    //     isoladamente e evitar duplicar essa lógica caso outra entrada (ex: uma API
    //     REST) venha a existir além do controller MVC.
    // EN: Check-in/check-out business rules — opening a stay occupies the room,
    //     finalizing frees it. Extracted from the controller so it's testable in
    //     isolation and won't get duplicated if another entry point (e.g. a REST API)
    //     is ever added besides the MVC controller.
    public interface IHospedagemService
    {
        Task<(bool Sucesso, string? Erro)> AbrirCheckInAsync(Hospedagem hospedagem);
        Task<ResultadoCheckOut> FinalizarCheckOutAsync(int hospedagemId);
    }
}
