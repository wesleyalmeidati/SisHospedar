using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Services
{
    public class ConsumoService : IConsumoService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ConsumoService> _logger;

        public ConsumoService(AppDbContext context, ILogger<ConsumoService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool Sucesso, string? Erro)> RegistrarAsync(Consumo consumo)
        {
            var hospedagem = await _context.Hospedagens.FindAsync(consumo.HospedagemId);
            if (hospedagem == null || hospedagem.Finalizada)
                return (false, "Hospedagem inválida ou já finalizada.");

            var produto = await _context.Produtos.FindAsync(consumo.ProdutoId);
            if (produto == null)
                return (false, "Produto não encontrado.");

            consumo.ValorUnitario = produto.Preco;

            // PT: Decremento atômico no banco (WHERE Estoque >= Quantidade) para evitar
            //     que duas requisições concorrentes deixem o estoque negativo (TOCTOU).
            // EN: Atomic DB decrement (WHERE Estoque >= Quantidade) to prevent two
            //     concurrent requests from pushing stock negative (TOCTOU race).
            var linhasAfetadas = await _context.Produtos
                .Where(p => p.ProdutoId == consumo.ProdutoId && p.Estoque >= consumo.Quantidade)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estoque, p => p.Estoque - consumo.Quantidade));

            // PT: Warning e não Information: estoque zerando é justamente o evento que a
            //     recepção precisa ver acontecendo com frequência para repor antes de faltar.
            // EN: Warning rather than Information: stock running out is exactly the event
            //     reception needs to see recurring, so they can restock before it runs dry.
            if (linhasAfetadas == 0)
            {
                _logger.LogWarning(
                    "Consumo recusado por estoque insuficiente: produto {ProdutoId}, quantidade pedida {Quantidade}, hospedagem {HospedagemId}.",
                    consumo.ProdutoId, consumo.Quantidade, consumo.HospedagemId);
                return (false, "Estoque insuficiente para este produto.");
            }

            _context.Add(consumo);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Consumo lançado: produto {ProdutoId} x{Quantidade} na hospedagem {HospedagemId}; estoque restante {Estoque}.",
                consumo.ProdutoId, consumo.Quantidade, consumo.HospedagemId, produto.Estoque - consumo.Quantidade);
            return (true, null);
        }
    }
}
