using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SisHospedar.Services
{
    // PT: Envia e-mail de verdade via SMTP (Gmail por padrão, com senha de app —
    //     ver seção "E-mail" do README para gerar uma). Configuração vem de
    //     appsettings/variáveis de ambiente (Email:Remetente, Email:SenhaApp,
    //     Email:SmtpHost, Email:SmtpPort), nunca hardcoded — mesmo padrão da
    //     connection string em Program.cs.
    // EN: Sends a real email via SMTP (Gmail by default, with an app password —
    //     see the README's "E-mail" section to generate one). Configuration comes
    //     from appsettings/environment variables (Email:Remetente, Email:SenhaApp,
    //     Email:SmtpHost, Email:SmtpPort), never hardcoded — same pattern as the
    //     connection string in Program.cs.
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public SmtpEmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task EnviarAsync(string destinatario, string assunto, string corpoHtml)
        {
            var remetente = _configuration["Email:Remetente"]
                ?? throw new InvalidOperationException("Configuração 'Email:Remetente' não definida.");
            var senhaApp = _configuration["Email:SenhaApp"]
                ?? throw new InvalidOperationException("Configuração 'Email:SenhaApp' não definida.");
            var host = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";
            var porta = int.Parse(_configuration["Email:SmtpPort"] ?? "587");

            var mensagem = new MimeMessage();
            mensagem.From.Add(MailboxAddress.Parse(remetente));
            mensagem.To.Add(MailboxAddress.Parse(destinatario));
            mensagem.Subject = assunto;
            mensagem.Body = new BodyBuilder { HtmlBody = corpoHtml }.ToMessageBody();

            using var cliente = new SmtpClient();
            await cliente.ConnectAsync(host, porta, SecureSocketOptions.StartTls);
            await cliente.AuthenticateAsync(remetente, senhaApp);
            await cliente.SendAsync(mensagem);
            await cliente.DisconnectAsync(true);
        }
    }
}
