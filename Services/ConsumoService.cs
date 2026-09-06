using Microsoft.EntityFrameworkCore;
using SisHospedar.Data;
using SisHospedar.Models;

namespace SisHospedar.Services
{
    public class ConsumoService : IConsumoService
    {
        private readonly AppDbContext _context;
        public ConsumoService(AppDbContext context) => _context = context;

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

            if (linhasAfetadas == 0)
                return (false, "Estoque insuficiente para este produto.");

            _context.Add(consumo);
            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
}
