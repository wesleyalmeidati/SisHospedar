using System.Net;
using Microsoft.EntityFrameworkCore;
using SisHospedar.Models;

namespace SisHospedar.Tests;

// PT: Todas as telas de exclusão do projeto estavam sem teste, e quatro delas tinham bug —
//     encontrados numa varredura deliberada, não por acaso.
//
//     Três eram a mesma causa: Reserva e Hospedagem apontam para Hospede e Apartamento com
//     DeleteBehavior.Restrict, e Consumo aponta para Produto do mesmo jeito. O controller
//     fazia Remove + SaveChangesAsync sem checar nada, então o banco recusava o DELETE, o
//     DbUpdateException subia sem tratamento e o usuário recebia uma página 500 — sem
//     explicação do que houve nem do que fazer. Excluir é operação de uma tecla e o erro
//     não dava nenhuma pista.
//
//     O quarto era pior e de outra natureza: dava para excluir a PRÓPRIA conta logado, e
//     dava para excluir a última conta com acesso à gestão. O segundo caso é irreversível
//     pela aplicação — sem nenhum Administrador ou Gerente, ninguém consegue cadastrar
//     funcionário, nem recriar o próprio acesso. Só mexendo direto no banco.
// EN: Every deletion screen in the project was untested, and four of them had bugs — found
//     in a deliberate sweep, not by accident.
//
//     Three shared one cause: Reserva and Hospedagem point at Hospede and Apartamento with
//     DeleteBehavior.Restrict, and Consumo points at Produto the same way. The controller did
//     Remove + SaveChangesAsync without checking anything, so the database refused the
//     DELETE, the DbUpdateException went unhandled and the user got a 500 page — with no
//     explanation of what happened or what to do. Deleting is a one-keystroke operation and
//     the error gave no clue at all.
//
//     The fourth was worse and of a different kind: you could delete your OWN account while
//     logged in, and you could delete the last account with management access. The second is
//     irreversible through the application — with no Administrador or Gerente left, nobody
//     can register an employee, nor recreate their own access. Only by touching the database.
public class ExclusaoProtegidaTests : TestesIntegracaoBase
{
    public ExclusaoProtegidaTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static string NumeroUnico(char prefixo) => $"{prefixo}{Guid.NewGuid():N}"[..9];

    private async Task<HttpClient> GestorLogadoAsync()
    {
        var client = CriarCliente();
        await LoginComoAsync(client, "luan.santos", "Senha123");
        return client;
    }

    // PT: A tela Delete/{id} é quem emite o token de antiforgery do formulário de confirmação.
    // EN: The Delete/{id} screen is what issues the confirmation form's antiforgery token.
    private Task<HttpResponseMessage> ConfirmarExclusaoAsync(HttpClient client, string controlador, int id) =>
        PostComAntiforgeryAsync(client, $"/{controlador}/Delete/{id}", $"/{controlador}/Delete/{id}",
            new Dictionary<string, string> { ["id"] = id.ToString() });

    [Fact]
    public async Task ExcluirHospedeComHospedagem_NaoEstoura500_ENaoRemove()
    {
        var hospedeId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = new Hospede
            {
                Nome = "Hospede Com Historico",
                Cpf = Guid.NewGuid().ToString("N")[..14],
                Telefone = "(41) 90000-0000",
                DataNascimento = new DateTime(1990, 1, 1)
            };
            var apartamento = new Apartamento
            {
                Numero = NumeroUnico('H'), Capacidade = 2, ValorDiaria = 150m,
                Status = StatusApartamento.Ocupado
            };
            db.Hospedes.Add(hospede);
            db.Apartamentos.Add(apartamento);
            await db.SaveChangesAsync();

            db.Hospedagens.Add(new Hospedagem
            {
                HospedeId = hospede.HospedeId,
                ApartamentoId = apartamento.ApartamentoId,
                DataEntrada = DateTime.Today.AddDays(-2)
            });
            await db.SaveChangesAsync();
            hospedeId = hospede.HospedeId;
        });

        var client = await GestorLogadoAsync();
        var resposta = await ConfirmarExclusaoAsync(client, "Hospede", hospedeId);

        // PT: O que falhava aqui era InternalServerError. O redirect é o comportamento certo:
        //     a recepção volta para a lista com uma mensagem explicando o motivo.
        // EN: What used to happen here was InternalServerError. The redirect is the correct
        //     behavior: reception returns to the list with a message explaining why.
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        await Factory.UsandoContextoAsync(async db =>
            Assert.True(await db.Hospedes.AnyAsync(h => h.HospedeId == hospedeId),
                "O hóspede com histórico não pode ser removido."));
    }

    [Fact]
    public async Task ExcluirApartamentoComReserva_NaoEstoura500_ENaoRemove()
    {
        int apartamentoId = 0, hospedeId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = new Hospede
            {
                Nome = "Hospede Reserva",
                Cpf = Guid.NewGuid().ToString("N")[..14],
                Telefone = "(41) 90000-0000",
                DataNascimento = new DateTime(1990, 1, 1)
            };
            var apartamento = new Apartamento
            {
                Numero = NumeroUnico('A'), Capacidade = 2, ValorDiaria = 150m,
                Status = StatusApartamento.Reservado
            };
            db.Hospedes.Add(hospede);
            db.Apartamentos.Add(apartamento);
            await db.SaveChangesAsync();

            db.Reservas.Add(new Reserva
            {
                HospedeId = hospede.HospedeId,
                ApartamentoId = apartamento.ApartamentoId,
                DataEntrada = DateTime.Today.AddDays(5),
                DataSaidaPrevista = DateTime.Today.AddDays(8),
                Status = StatusReserva.Pendente
            });
            await db.SaveChangesAsync();
            apartamentoId = apartamento.ApartamentoId;
            hospedeId = hospede.HospedeId;
        });

        var client = await GestorLogadoAsync();
        var resposta = await ConfirmarExclusaoAsync(client, "Apartamento", apartamentoId);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        await Factory.UsandoContextoAsync(async db =>
            Assert.True(await db.Apartamentos.AnyAsync(a => a.ApartamentoId == apartamentoId)));
    }

    [Fact]
    public async Task ExcluirProdutoJaConsumido_NaoEstoura500_ENaoRemove()
    {
        var produtoId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = new Hospede
            {
                Nome = "Hospede Consumo",
                Cpf = Guid.NewGuid().ToString("N")[..14],
                Telefone = "(41) 90000-0000",
                DataNascimento = new DateTime(1990, 1, 1)
            };
            var apartamento = new Apartamento
            {
                Numero = NumeroUnico('P'), Capacidade = 2, ValorDiaria = 150m,
                Status = StatusApartamento.Ocupado
            };
            var produto = new Produto { Nome = "Item Consumido", Preco = 10m, Estoque = 50 };
            db.Hospedes.Add(hospede);
            db.Apartamentos.Add(apartamento);
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();

            var hospedagem = new Hospedagem
            {
                HospedeId = hospede.HospedeId,
                ApartamentoId = apartamento.ApartamentoId,
                DataEntrada = DateTime.Today
            };
            db.Hospedagens.Add(hospedagem);
            await db.SaveChangesAsync();

            db.Consumos.Add(new Consumo
            {
                HospedagemId = hospedagem.HospedagemId,
                ProdutoId = produto.ProdutoId,
                Quantidade = 1,
                ValorUnitario = 10m
            });
            await db.SaveChangesAsync();
            produtoId = produto.ProdutoId;
        });

        var client = await GestorLogadoAsync();
        var resposta = await ConfirmarExclusaoAsync(client, "Produto", produtoId);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        await Factory.UsandoContextoAsync(async db =>
            Assert.True(await db.Produtos.AnyAsync(p => p.ProdutoId == produtoId)));
    }

    [Fact]
    public async Task ExcluirSemVinculos_Funciona()
    {
        // PT: O contraponto necessário. Sem este teste, "não exclui nada nunca" passaria nos
        //     três acima e ninguém perceberia que a funcionalidade foi desligada por engano.
        // EN: The necessary counterpoint. Without this test, "never deletes anything" would
        //     pass all three above and nobody would notice the feature had been switched off
        //     by mistake.
        var produtoId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var produto = new Produto { Nome = "Item Nunca Consumido", Preco = 5m, Estoque = 10 };
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();
            produtoId = produto.ProdutoId;
        });

        var client = await GestorLogadoAsync();
        var resposta = await ConfirmarExclusaoAsync(client, "Produto", produtoId);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        await Factory.UsandoContextoAsync(async db =>
            Assert.False(await db.Produtos.AnyAsync(p => p.ProdutoId == produtoId)));
    }

    [Fact]
    public async Task FuncionarioNaoConsegueExcluirAPropriaConta()
    {
        var meuId = 0;
        await Factory.UsandoContextoAsync(async db =>
            meuId = (await db.Funcionarios.FirstAsync(f => f.Login == "luan.santos")).FuncionarioId);

        var client = await GestorLogadoAsync();
        var resposta = await ConfirmarExclusaoAsync(client, "Funcionario", meuId);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);

        // PT: Antes isto removia a conta e deixava a pessoa com um cookie válido apontando
        //     para um funcionário inexistente — autenticada para o handler, inexistente para
        //     a aplicação.
        // EN: This used to remove the account and leave the person with a valid cookie
        //     pointing at an employee who no longer exists — authenticated as far as the
        //     handler is concerned, nonexistent to the application.
        await Factory.UsandoContextoAsync(async db =>
            Assert.True(await db.Funcionarios.AnyAsync(f => f.FuncionarioId == meuId),
                "A própria conta não pode ser excluída pelo usuário logado."));
    }

    [Fact]
    public async Task ExcluirOutroFuncionarioContinuaFuncionando()
    {
        // PT: Contraponto da regra de autoexclusão: ela não pode ter desligado a exclusão
        //     inteira. Sem este teste, um "return sempre com erro" passaria no teste acima
        //     e ninguém notaria que a tela parou de funcionar.
        //
        //     Este teste substituiu um que eu tinha escrito para uma regra de "não excluir o
        //     último gestor". Ao rodar a suíte com as proteções revertidas, ele foi o único
        //     que NÃO falhou — porque não testava nada: a regra que ele deveria cobrir é
        //     inalcançável. Quem exclui é sempre gestor (ver [RequerCargo] no controller) e
        //     não pode excluir a si mesmo, então excluir outra pessoa sempre deixa pelo menos
        //     ele. A contagem nunca chega a zero, e a regra virou código morto — removida.
        // EN: The counterpoint to the self-deletion rule: it must not have switched deletion
        //     off entirely. Without this test, a "always return an error" would pass the test
        //     above and nobody would notice the screen stopped working.
        //
        //     This test replaced one I had written for a "don't delete the last manager" rule.
        //     Running the suite with the protections reverted, it was the only one that did
        //     NOT fail — because it tested nothing: the rule it was meant to cover is
        //     unreachable. Whoever deletes is always a manager (see [RequerCargo] on the
        //     controller) and can't delete themselves, so deleting anyone else always leaves
        //     at least them. The count never reaches zero, the rule was dead code, and it was
        //     removed.
        var outroId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var outro = new Funcionario
            {
                Nome = "Funcionario Descartavel",
                Cargo = CargoFuncionario.Limpeza,
                Login = $"tmp{Guid.NewGuid():N}"[..20],
                SenhaHash = "x",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            db.Funcionarios.Add(outro);
            await db.SaveChangesAsync();
            outroId = outro.FuncionarioId;
        });

        var client = await GestorLogadoAsync();
        var resposta = await ConfirmarExclusaoAsync(client, "Funcionario", outroId);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        await Factory.UsandoContextoAsync(async db =>
            Assert.False(await db.Funcionarios.AnyAsync(f => f.FuncionarioId == outroId)));
    }
}
