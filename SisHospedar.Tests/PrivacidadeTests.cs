using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SisHospedar.Models;
using SisHospedar.Services;

namespace SisHospedar.Tests;

// PT: Testes da eliminação prevista na LGPD (Art. 18, VI). A regra central não é
//     "apagar": é decidir entre apagar de verdade e anonimizar preservando o registro
//     da hospedagem, que a legislação fiscal obriga a guardar (Art. 16). Esses testes
//     existem para que essa distinção não se perca numa alteração futura — trocar
//     anonimização por exclusão apagaria histórico fiscal sem ninguém notar.
// EN: Tests for LGPD erasure (Art. 18, VI). The core rule isn't "delete": it's choosing
//     between a genuine delete and anonymizing while preserving the stay record, which
//     tax law requires retaining (Art. 16). These tests exist so that distinction isn't
//     lost in a future change — swapping anonymization for deletion would silently erase
//     financial history.
public class PrivacidadeTests : TestesIntegracaoBase
{
    public PrivacidadeTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<T> UsandoServicoAsync<T>(Func<IPrivacidadeService, Task<T>> acao)
    {
        using var scope = Factory.Services.CreateScope();
        var servico = scope.ServiceProvider.GetRequiredService<IPrivacidadeService>();
        return await acao(servico);
    }

    private static Hospede NovoHospede(string nome) => new()
    {
        Nome = nome,
        Cpf = Guid.NewGuid().ToString("N")[..14],
        Telefone = "(41) 99999-0000",
        DataNascimento = new DateTime(1990, 5, 20),
        Email = $"{Guid.NewGuid():N}@exemplo.com",
        Login = $"login{Guid.NewGuid():N}"[..20]
    };

    private async Task<int> CriarHospedeAsync(string nome)
    {
        var id = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = NovoHospede(nome);
            db.Hospedes.Add(hospede);
            await db.SaveChangesAsync();
            id = hospede.HospedeId;
        });
        return id;
    }

    private async Task<(int hospedeId, int hospedagemId)> CriarHospedeComEstadiaAsync(bool finalizada)
    {
        int hospedeId = 0, hospedagemId = 0;
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = NovoHospede("Titular Com Estadia");
            var apartamento = new Apartamento
            {
                Numero = $"P{Guid.NewGuid():N}"[..9],
                Capacidade = 2,
                ValorDiaria = 150m,
                Status = finalizada ? StatusApartamento.Disponivel : StatusApartamento.Ocupado
            };
            db.Hospedes.Add(hospede);
            db.Apartamentos.Add(apartamento);
            await db.SaveChangesAsync();

            var hospedagem = new Hospedagem
            {
                HospedeId = hospede.HospedeId,
                ApartamentoId = apartamento.ApartamentoId,
                DataEntrada = DateTime.Today.AddDays(-3),
                DataSaida = finalizada ? DateTime.Today : null,
                Finalizada = finalizada
            };
            db.Hospedagens.Add(hospedagem);
            await db.SaveChangesAsync();

            hospedeId = hospede.HospedeId;
            hospedagemId = hospedagem.HospedagemId;
        });
        return (hospedeId, hospedagemId);
    }

    [Fact]
    public async Task ExcluirConta_SemHistorico_ApagaOCadastroDeVerdade()
    {
        var hospedeId = await CriarHospedeAsync("Titular Sem Histórico");

        var resultado = await UsandoServicoAsync(s => s.ExcluirContaAsync(hospedeId));

        Assert.Equal(ResultadoExclusao.Excluido, resultado);
        await Factory.UsandoContextoAsync(async db =>
            Assert.False(await db.Hospedes.AnyAsync(h => h.HospedeId == hospedeId)));
    }

    [Fact]
    public async Task ExcluirConta_ComEstadiaFinalizada_AnonimizaEPreservaORegistro()
    {
        var (hospedeId, hospedagemId) = await CriarHospedeComEstadiaAsync(finalizada: true);

        var resultado = await UsandoServicoAsync(s => s.ExcluirContaAsync(hospedeId));

        Assert.Equal(ResultadoExclusao.Anonimizado, resultado);

        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = await db.Hospedes.FirstOrDefaultAsync(h => h.HospedeId == hospedeId);
            Assert.NotNull(hospede);

            // Nenhum dado pessoal sobrou.
            Assert.Equal("Titular removido", hospede!.Nome);
            Assert.Null(hospede.Email);
            Assert.Null(hospede.Login);
            Assert.Null(hospede.SenhaHash);
            Assert.Equal(string.Empty, hospede.Telefone);
            Assert.DoesNotContain(".", hospede.Cpf);

            // PT: E o registro da hospedagem continua existindo — é a metade da regra que
            //     uma "simplificação" futura poderia apagar sem perceber.
            // EN: And the stay record still exists — the half of the rule a future
            //     "simplification" could erase without noticing.
            Assert.True(await db.Hospedagens.AnyAsync(h => h.HospedagemId == hospedagemId));
        });
    }

    [Fact]
    public async Task ExcluirConta_ComEstadiaAberta_ERecusada()
    {
        var (hospedeId, _) = await CriarHospedeComEstadiaAsync(finalizada: false);

        var resultado = await UsandoServicoAsync(s => s.ExcluirContaAsync(hospedeId));

        Assert.Equal(ResultadoExclusao.BloqueadoEstadiaAtiva, resultado);
        await Factory.UsandoContextoAsync(async db =>
        {
            var hospede = await db.Hospedes.FirstAsync(h => h.HospedeId == hospedeId);
            Assert.Equal("Titular Com Estadia", hospede.Nome);
        });
    }

    [Fact]
    public async Task ExcluirConta_ComReservaPendente_ERecusada()
    {
        var hospedeId = await CriarHospedeAsync("Titular Com Reserva");
        await Factory.UsandoContextoAsync(async db =>
        {
            var apartamento = new Apartamento
            {
                Numero = $"R{Guid.NewGuid():N}"[..9],
                Capacidade = 2,
                ValorDiaria = 150m,
                Status = StatusApartamento.Reservado
            };
            db.Apartamentos.Add(apartamento);
            await db.SaveChangesAsync();

            db.Reservas.Add(new Reserva
            {
                HospedeId = hospedeId,
                ApartamentoId = apartamento.ApartamentoId,
                DataEntrada = DateTime.Today.AddDays(5),
                DataSaidaPrevista = DateTime.Today.AddDays(8),
                Status = StatusReserva.Pendente
            });
            await db.SaveChangesAsync();
        });

        var resultado = await UsandoServicoAsync(s => s.ExcluirContaAsync(hospedeId));

        Assert.Equal(ResultadoExclusao.BloqueadoReservaPendente, resultado);
    }

    [Fact]
    public async Task ReunirDados_TrazCadastroEHistorico_SemExporSenha()
    {
        var (hospedeId, _) = await CriarHospedeComEstadiaAsync(finalizada: true);

        var dados = await UsandoServicoAsync(s => s.ReunirDadosAsync(hospedeId));

        Assert.NotNull(dados);
        Assert.Equal("Titular Com Estadia", dados!.Nome);
        Assert.Single(dados.Hospedagens);

        // PT: DadosPessoaisHospede é um tipo próprio justamente para não ter onde
        //     acidentalmente vazar hash de senha ou security stamp na exportação.
        // EN: DadosPessoaisHospede is a dedicated type precisely so there's nowhere for a
        //     password hash or security stamp to accidentally leak into the export.
        var propriedades = typeof(DadosPessoaisHospede).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("SenhaHash", propriedades);
        Assert.DoesNotContain("SecurityStamp", propriedades);
    }
}
