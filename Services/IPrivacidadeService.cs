namespace SisHospedar.Services
{
    // PT: Resultado da tentativa de exclusão de conta. São três desfechos diferentes
    //     porque a LGPD não trata "excluir" como uma operação única: o Art. 16 permite
    //     reter dados para cumprir obrigação legal (no caso de hotel, o registro da
    //     hospedagem e os lançamentos fiscais), e o Art. 12 trata dado anonimizado como
    //     não-pessoal. Quem nunca se hospedou não gerou registro a preservar e pode ser
    //     removido de fato.
    // EN: Outcome of an account deletion attempt. Three different results because LGPD
    //     doesn't treat "delete" as a single operation: Art. 16 allows retaining data to
    //     comply with a legal obligation (for a hotel, the stay record and its financial
    //     entries), and Art. 12 treats anonymized data as non-personal. A guest who never
    //     stayed generated no record worth preserving and can genuinely be removed.
    public enum ResultadoExclusao
    {
        Excluido,
        Anonimizado,
        BloqueadoEstadiaAtiva,
        BloqueadoReservaPendente,
        NaoEncontrado
    }

    // PT: Retrato do que o sistema guarda sobre um hóspede, montado para os direitos de
    //     acesso (Art. 18, II) e portabilidade (Art. 18, V). É um tipo próprio, e não a
    //     entidade Hospede, justamente para nunca expor SenhaHash/SecurityStamp.
    // EN: A snapshot of what the system holds about a guest, assembled for the access
    //     (Art. 18, II) and portability (Art. 18, V) rights. It's a dedicated type rather
    //     than the Hospede entity specifically so SenhaHash/SecurityStamp are never exposed.
    public record DadosPessoaisHospede(
        int Id,
        string Nome,
        string Cpf,
        string Telefone,
        DateTime DataNascimento,
        string? Email,
        string? Login,
        IReadOnlyList<ReservaExportada> Reservas,
        IReadOnlyList<HospedagemExportada> Hospedagens);

    public record ReservaExportada(
        int Id,
        string Apartamento,
        DateTime DataEntrada,
        DateTime DataSaidaPrevista,
        string Situacao);

    public record HospedagemExportada(
        int Id,
        string Apartamento,
        DateTime DataEntrada,
        DateTime? DataSaida,
        bool Finalizada,
        IReadOnlyList<ConsumoExportado> Consumos);

    public record ConsumoExportado(
        string Produto,
        int Quantidade,
        decimal ValorUnitario,
        decimal ValorTotal);

    public interface IPrivacidadeService
    {
        Task<DadosPessoaisHospede?> ReunirDadosAsync(int hospedeId);
        Task<ResultadoExclusao> ExcluirContaAsync(int hospedeId);
    }
}
