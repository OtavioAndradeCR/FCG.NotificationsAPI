namespace FCG.NotificationsAPI.Events;

/// <summary>
/// Publicado pela UsersAPI quando um novo usuário padrão é criado.
/// </summary>
public class UserCreatedEvent
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }

    /// <summary>
    /// Preenchido apenas quando IsAdmin = true.
    /// </summary>
    public string? TemporaryPassword { get; set; }
}
