using System.Threading.Tasks;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace Microsoft.eShopWeb.Infrastructure.Services;

// This class is used by the application to send email for account confirmation and password reset.
// For more details see https://go.microsoft.com/fwlink/?LinkID=532713
public class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ILogger<EmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string message)
    {
        _logger.LogInformation("Email requested to {Email} with subject {Subject}.", email, subject);
        // TODO: Wire this up to actual email sending logic via SendGrid, local SMTP, etc.
        return Task.CompletedTask;
    }
}
