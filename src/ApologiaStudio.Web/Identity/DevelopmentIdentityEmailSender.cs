using ApologiaStudio.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;

namespace ApologiaStudio.Web.Identity;

internal sealed class DevelopmentIdentityEmailSender(
    ILogger<DevelopmentIdentityEmailSender> logger)
    : IEmailSender<ApologiaIdentityUser>
{
    public Task SendConfirmationLinkAsync(
        ApologiaIdentityUser user,
        string email,
        string confirmationLink)
    {
        logger.LogInformation(
            "Development e-mail confirmation prepared for {Email}.",
            email);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(
        ApologiaIdentityUser user,
        string email,
        string resetLink) =>
        Task.CompletedTask;

    public Task SendPasswordResetCodeAsync(
        ApologiaIdentityUser user,
        string email,
        string resetCode) =>
        Task.CompletedTask;
}
