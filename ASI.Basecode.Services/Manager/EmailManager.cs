using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
namespace ASI.Basecode.Services.Manager;
public class EmailManager : IEmailManager
{
    private readonly IConfiguration configuration;
    public EmailManager(IConfiguration configuration) { this.configuration = configuration; }
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(configuration["Smtp:From"]));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };
        using var client = new SmtpClient { Timeout = 15000 };
        await client.ConnectAsync(configuration["Smtp:Host"], int.Parse(configuration["Smtp:Port"]), SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(configuration["Smtp:User"], configuration["Smtp:Password"].Replace(" ", ""), cancellationToken);
        await client.SendAsync(message, cancellationToken);
        // The server has accepted the message; disconnect failure must not report delivery failure.
        try { await client.DisconnectAsync(true, cancellationToken); } catch { }
    }
}
