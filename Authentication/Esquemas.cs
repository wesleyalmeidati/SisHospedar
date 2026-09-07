namespace SisHospedar.Authentication
{
    // PT: Nomes dos dois esquemas de cookie de autenticação — um por identidade,
    //     mantendo Funcionario e Hospede completamente independentes (cada um com seu
    //     próprio cookie, login, expiração). Nenhum dos dois é o esquema "padrão":
    //     todo [Authorize] neste projeto especifica explicitamente qual usar.
    // EN: Names of the two cookie authentication schemes — one per identity, keeping
    //     Funcionario and Hospede fully independent (each with its own cookie, login,
    //     expiration). Neither is the "default" scheme: every [Authorize] in this
    //     project explicitly states which one to use.
    public static class Esquemas
    {
        public const string Funcionario = "Funcionario";
        public const string Hospede = "Hospede";
    }
}
