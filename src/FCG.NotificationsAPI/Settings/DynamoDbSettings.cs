namespace FCG.NotificationsAPI.Settings;

public class DynamoDbSettings
{
    public const string SectionName = "DynamoDB";

    /// <summary>
    /// Nome da tabela DynamoDB que armazena o espelho de usuários.
    /// </summary>
    public string TableName { get; set; } = "fcg-notification-users";

    /// <summary>
    /// URL do DynamoDB Local (dev offline). Vazio = usar AWS real.
    /// Ex.: http://localhost:8000
    /// </summary>
    public string? ServiceURL { get; set; }
}