namespace FCG.NotificationsAPI.Data;

/// <summary>
/// Nomes dos atributos da tabela DynamoDB — centralizados para
/// evitar magic strings espalhadas pelo código.
/// </summary>
public static class DynamoDbConstants
{
    public const string AttrId = "Id";
    public const string AttrName = "Name";
    public const string AttrEmail = "Email";
    public const string AttrIsAdmin = "IsAdmin";
    public const string AttrCreatedAt = "CreatedAt";
}