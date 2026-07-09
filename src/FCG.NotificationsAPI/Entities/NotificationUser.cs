namespace FCG.NotificationsAPI.Entities;

/// <summary>
/// Espelho local de usuários persistido ao consumir UserCreatedEvent.
/// Permite que outros serviços consultem email/nome passando apenas o UserId.
/// </summary>
public class NotificationUser
{
    public Guid Id { get; set; }        // mesmo Id vindo do UsersAPI
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
}
