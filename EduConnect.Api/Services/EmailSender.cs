using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace EduConnect.Api.Services;

public class EmailSender
{
    private readonly IConfiguration _config;

    public EmailSender(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var host = _config["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host não configurado");
        var portStr = _config["Smtp:Port"] ?? "587";
        var user = _config["Smtp:User"] ?? throw new InvalidOperationException("Smtp:User não configurado");
        var pass = _config["Smtp:Pass"] ?? throw new InvalidOperationException("Smtp:Pass não configurado");
        var fromName = _config["Smtp:FromName"] ?? "EduConnect";

        if (!int.TryParse(portStr, out var port)) port = 587;

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass)
        };

        using var msg = new MailMessage
        {
            From = new MailAddress(user, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        msg.To.Add(toEmail);

        await client.SendMailAsync(msg);
    }
}
