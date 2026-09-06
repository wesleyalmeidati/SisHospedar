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
