namespace FCG.NotificationsAPI.Services;

public interface IEmailNotificationService
{
    Task SendWelcomeAsync(string name, string email);
    Task SendWelcomeAdminAsync(string name, string email, string temporaryPassword);
    Task SendPurchaseConfirmedAsync(string name, string email, string gameTitle, decimal price, Guid orderId);
}
