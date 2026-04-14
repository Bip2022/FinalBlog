using System.Net;
using System.Net.Mail;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {

        // Pulling values from your configuration
        var email = _config["EmailSettings:Email"] ?? string.Empty;
        var password = _config["EmailSettings:Password"] ?? string.Empty;
        var host = _config["EmailSettings:Host"] ?? string.Empty;
        var port = int.Parse(_config["EmailSettings:Port"] ?? "587");

        var message = new MailMessage(email, to, subject, body);

        // Using 'using' ensures the connection is closed properly after use
        using var smtp = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(email, password),
            EnableSsl = true
        };

        await smtp.SendMailAsync(message);
    }
}