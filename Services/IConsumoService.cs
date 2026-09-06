using SisHospedar.Models;

namespace SisHospedar.Services
{
    // PT: Lança consumo de produto numa hospedagem ativa com baixa atômica de estoque
    //     (WHERE Estoque >= Quantidade), evitando a race condition TOCTOU entre duas
    //     requisições concorrentes.
    // EN: Records product consumption on an active stay with an atomic stock deduction
    //     (WHERE Estoque >= Quantidade), avoiding the TOCTOU race between two
    //     concurrent requests.
    public interface IConsumoService
    {
        Task<(bool Sucesso, string? Erro)> RegistrarAsync(Consumo consumo);
    }
}
