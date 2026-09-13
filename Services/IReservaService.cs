using SisHospedar.Models;

namespace SisHospedar.Services
{
    // PT: Regras de negócio do lado hóspede (criar pedido) e do lado recepção
    //     (confirmar = check-in / cancelar) das reservas.
    // EN: Business rules for both the guest side (create request) and the reception
    //     side (confirm = check-in / cancel) of bookings.
    public interface IReservaService
    {
        Task<(bool Sucesso, string? Erro)> CriarPedidoAsync(Reserva reserva);

        // PT: Só as regras de data, sem tocar o banco — devolve a mensagem de erro ou null.
        //     Existe porque o fluxo anônimo precisa validar ANTES de mandar a pessoa criar
        //     conta: as regras rodavam só dentro de CriarPedidoAsync, que no caminho anônimo
        //     só é alcançado depois do login. Quem digitasse o ano errado fazia todo o
        //     cadastro para só então descobrir. CriarPedidoAsync chama este mesmo método,
        //     então continua havendo uma fonte única da regra.
        // EN: The date rules only, touching no database — returns the error message or null.
        //     It exists because the anonymous flow has to validate BEFORE sending someone off
        //     to create an account: the rules used to live only inside CriarPedidoAsync,
        //     which on the anonymous path is reached only after login. Anyone mistyping the
        //     year completed the whole sign-up only to find out afterwards. CriarPedidoAsync
        //     calls this same method, so there is still a single source for the rule.
        string? ValidarDatas(Reserva reserva);

        // PT: Retorna a mensagem a exibir via TempData, ou null quando a reserva não
        //     existe mais ou já não está Pendente (o controller apenas redireciona
        //     em silêncio nesse caso, como no comportamento original).
        // EN: Returns the message to show via TempData, or null when the booking no
        //     longer exists or isn't Pendente anymore (the controller just redirects
        //     silently in that case, matching the original behavior).
        Task<string?> ConfirmarAsync(int reservaId);

        // PT: Retorna false se a reserva não existir mais ou já não estiver Pendente
        //     (o controller não exibe mensagem nesse caso, como no comportamento original).
        // EN: Returns false if the booking no longer exists or isn't Pendente anymore
        //     (the controller shows no message in that case, matching the original behavior).
        Task<bool> CancelarAsync(int reservaId);
    }
}
