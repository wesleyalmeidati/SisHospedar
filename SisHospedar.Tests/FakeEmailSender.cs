using System.Collections.Concurrent;
using SisHospedar.Services;

namespace SisHospedar.Tests;

// PT: Substitui SmtpEmailSender nos testes de integração (ver
//     CustomWebApplicationFactory) — captura o e-mail em memória em vez de
//     enviar de verdade, permitindo que um teste recupere o link de
//     redefinição de senha que teria ido por e-mail. ConcurrentBag porque a
//     factory (e portanto o container de DI) é compartilhada entre testes que
//     podem rodar em paralelo.
// EN: Replaces SmtpEmailSender in integration tests (see
//     CustomWebApplicationFactory) — captures the email in memory instead of
//     actually sending it, letting a test retrieve the password-reset link
//     that would have gone out by email. ConcurrentBag because the factory
//     (and therefore the DI container) is shared across tests that may run
//     in parallel.
public class FakeEmailSender : IEmailSender
{
    public record EmailEnviado(string Destinatario, string Assunto, string CorpoHtml);

    public ConcurrentBag<EmailEnviado> Enviados { get; } = new();

    public Task EnviarAsync(string destinatario, string assunto, string corpoHtml)
    {
        Enviados.Add(new EmailEnviado(destinatario, assunto, corpoHtml));
        return Task.CompletedTask;
    }
}
