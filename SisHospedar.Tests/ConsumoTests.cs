using System.Net;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Models;

namespace SisHospedar.Tests;

// PT: Testes de integração para a baixa de estoque em ConsumoController.Create,
//     incluindo a proteção contra TOCTOU (decremento atômico) e as validações de
//     hospedagem ativa / estoque suficiente.
// EN: Integration tests for the stock deduction in ConsumoController.Create,
//     including the TOCTOU guard (atomic decrement) and the active-stay /
//     sufficient-stock validations.
public class ConsumoTests : TestesIntegracaoBase
{
    public ConsumoTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<(int hospedagemId, int produtoId)> CriarHospedagemEProdutoAsync(int estoque, bool finalizada = false)
    {
        int hospedagemId = 0, produtoId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = new Hospede
            {
                Nome = "Teste Consumo",
                Cpf = Guid.NewGuid().ToString("N")[..14],
                DataNascimento = new DateTime(1990, 1, 1)
            };
            var apartamento = new Apartamento
            {
                Numero = $"C{Guid.NewGuid():N}"[..9],
                Capacidade = 2,
                ValorDiaria = 100m,
                Status = StatusApartamento.Ocupado
            };
            var produto = new Produto { Nome = "Produto Teste", Preco = 10m, Estoque = estoque };
            db.Hospedes.Add(hospede);
            db.Apartamentos.Add(apartamento);
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();

            var hospedagem = new Hospedagem
            {
                HospedeId = hospede.HospedeId,
                ApartamentoId = apartamento.ApartamentoId,
                DataEntrada = DateTime.Today,
                Finalizada = finalizada
            };
            db.Hospedagens.Add(hospedagem);
            await db.SaveChangesAsync();

            hospedagemId = hospedagem.HospedagemId;
            produtoId = produto.ProdutoId;
        });
        return (hospedagemId, produtoId);
    }

    [Fact]
    public async Task Create_DecrementaEstoqueDoProduto()
    {
        var (hospedagemId, produtoId) = await CriarHospedagemEProdutoAsync(estoque: 10);
        var client = CriarCliente();
        await LoginComoAsync(client, "matias.sampaio", "Senha123");

        var resposta = await PostComAntiforgeryAsync(client,
            $"/Consumo/Create?hospedagemId={hospedagemId}", "/Consumo/Create",
            new Dictionary<string, string>
            {
                ["HospedagemId"] = hospedagemId.ToString(),
                ["ProdutoId"] = produtoId.ToString(),
                ["Quantidade"] = "3"
            });

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var produto = await db.Produtos.FindAsync(produtoId);
            Assert.Equal(7, produto!.Estoque);

            var consumo = await db.Consumos.SingleAsync(c => c.HospedagemId == hospedagemId);
            Assert.Equal(3, consumo.Quantidade);
            Assert.Equal(10m, consumo.ValorUnitario);
        });
    }

    [Fact]
    public async Task Create_NaoDecrementaEstoqueQuandoInsuficiente()
    {
        var (hospedagemId, produtoId) = await CriarHospedagemEProdutoAsync(estoque: 2);
        var client = CriarCliente();
        await LoginComoAsync(client, "matias.sampaio", "Senha123");

        var resposta = await PostComAntiforgeryAsync(client,
            $"/Consumo/Create?hospedagemId={hospedagemId}", "/Consumo/Create",
            new Dictionary<string, string>
            {
                ["HospedagemId"] = hospedagemId.ToString(),
                ["ProdutoId"] = produtoId.ToString(),
                ["Quantidade"] = "5"
            });

        // PT: Falha de validação re-renderiza o form (200), não redireciona.
        // EN: Validation failure re-renders the form (200), it doesn't redirect.
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var produto = await db.Produtos.FindAsync(produtoId);
            Assert.Equal(2, produto!.Estoque);
            Assert.False(await db.Consumos.AnyAsync(c => c.HospedagemId == hospedagemId));
        });
    }

    [Fact]
    public async Task Create_RejeitaHospedagemJaFinalizada()
    {
        var (hospedagemId, produtoId) = await CriarHospedagemEProdutoAsync(estoque: 10, finalizada: true);
        var client = CriarCliente();
        await LoginComoAsync(client, "matias.sampaio", "Senha123");

        var resposta = await PostComAntiforgeryAsync(client,
            $"/Consumo/Create?hospedagemId={hospedagemId}", "/Consumo/Create",
            new Dictionary<string, string>
            {
                ["HospedagemId"] = hospedagemId.ToString(),
                ["ProdutoId"] = produtoId.ToString(),
                ["Quantidade"] = "1"
            });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
        {
            var produto = await db.Produtos.FindAsync(produtoId);
            Assert.Equal(10, produto!.Estoque);
        });
    }
}
