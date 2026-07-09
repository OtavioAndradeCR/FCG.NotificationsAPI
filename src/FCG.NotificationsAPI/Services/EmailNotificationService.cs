using FCG.NotificationsAPI.Settings;
using HandlebarsDotNet;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FCG.NotificationsAPI.Services;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<SmtpSettings> smtpOptions,
        ILogger<EmailNotificationService> logger)
    {
        _smtp = smtpOptions.Value;
        _logger = logger;
    }

    public async Task SendWelcomeAsync(string name, string email)
    {
        var body = await RenderTemplateAsync("email-welcome", new
        {
            Name = name,
            Body = "Bem-vindo(a) ao sistema que vai levar você a diversas jornadas e experiências gamer."
        });

        await SendAsync(
            to: email,
            toName: name,
            subject: "Bem-vindo(a) ao sistema Fiap Cloud Games",
            htmlBody: body);
    }

    public async Task SendWelcomeAdminAsync(string name, string email, string temporaryPassword)
    {
        var body = await RenderTemplateAsync("email-welcome-admin", new
        {
            Name = name,
            TemporaryPassword = temporaryPassword
        });

        await SendAsync(
            to: email,
            toName: name,
            subject: "Bem-vindo(a) ao sistema Fiap Cloud Games - sua senha temporária",
            htmlBody: body);
    }

    public async Task SendPurchaseConfirmedAsync(
        string name, string email, string gameTitle, decimal price, Guid orderId)
    {
        var body = await RenderTemplateAsync("email-purchase-confirmed", new
        {
            Name = name,
            GameTitle = gameTitle,
            Price = price.ToString("C2", new System.Globalization.CultureInfo("pt-BR")),
            OrderId = orderId.ToString()
        });

        await SendAsync(
            to: email,
            toName: name,
            subject: $"Compra confirmada: {gameTitle}",
            htmlBody: body);
    }

    // ------------------------------------------------------------------ //

    private async Task SendAsync(string to, string toName, string subject, string htmlBody)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_smtp.SenderName, _smtp.SenderEmail));
        message.To.Add(new MailboxAddress(toName, to));
        message.Subject = subject;

        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            // StartTls é o modo correto para Gmail na porta 587
            await client.ConnectAsync(_smtp.Host, _smtp.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_smtp.Username, _smtp.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(quit: true);

            _logger.LogInformation(
                "[EMAIL ENVIADO] Para: {To} | Assunto: {Subject}",
                to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[EMAIL ERRO] Falha ao enviar para: {To} | Assunto: {Subject}",
                to, subject);

            // Re-lança para o MassTransit retentar a mensagem
            throw;
        }
    }

    private static async Task<string> RenderTemplateAsync(string templateName, object data)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Templates",
            "Hbs",
            $"{templateName}.hbs");

        var source = await File.ReadAllTextAsync(path);
        var template = Handlebars.Compile(source);
        return template(data);
    }
}
