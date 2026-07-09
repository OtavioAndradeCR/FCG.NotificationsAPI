namespace FCG.NotificationsAPI.Settings;

public class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Para Gmail: use uma App Password gerada em
    /// https://myaccount.google.com/apppasswords
    /// (requer autenticação de 2 fatores ativa na conta).
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Nome exibido no campo "De:" dos e-mails enviados.
    /// </summary>
    public string SenderName { get; set; } = "FIAP Cloud Games";

    /// <summary>
    /// Endereço exibido no campo "De:". 
    /// Geralmente igual ao Username para Gmail.
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;
}
