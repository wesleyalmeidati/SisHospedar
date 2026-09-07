namespace SisHospedar.Services
{
    // PT: Abstração de envio de e-mail — hoje só usada pelo fluxo de "esqueci
    //     minha senha" (ContaController). SmtpEmailSender é a implementação real
    //     (Gmail via MailKit); nos testes de integração, CustomWebApplicationFactory
    //     troca por uma fake que só guarda o e-mail em memória, sem enviar nada.
    // EN: Email-sending abstraction — currently only used by the "forgot my
    //     password" flow (ContaController). SmtpEmailSender is the real
    //     implementation (Gmail via MailKit); integration tests swap it for a
    //     fake that just captures the email in memory instead of sending it.
    public interface IEmailSender
    {
        Task EnviarAsync(string destinatario, string assunto, string corpoHtml);
    }
}
