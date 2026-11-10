using FCG.NotificationsAPI.Entities;
using FCG.NotificationsAPI.Repositories;
using FCG.NotificationsAPI.Services;

namespace FCG.NotificationsAPI.Tests;

public class FakeEmailService : IEmailNotificationService
{
    public List<(string Name, string Email)> Welcome { get; } = new();
    public List<(string Name, string Email, string Password)> Admin { get; } = new();
    public List<(string Name, string Email, string Game, decimal Price, Guid OrderId)> Purchases { get; } = new();

    public Task SendWelcomeAsync(string name, string email)
    {
        Welcome.Add((name, email));
        return Task.CompletedTask;
    }

    public Task SendWelcomeAdminAsync(string name, string email, string temporaryPassword)
    {
        Admin.Add((name, email, temporaryPassword));
        return Task.CompletedTask;
    }

    public Task SendPurchaseConfirmedAsync(string name, string email, string gameTitle, decimal price, Guid orderId)
    {
        Purchases.Add((name, email, gameTitle, price, orderId));
        return Task.CompletedTask;
    }

    public int Total => Welcome.Count + Admin.Count + Purchases.Count;
}

public class FakeUserRepository : INotificationUserRepository
{
    public Dictionary<Guid, NotificationUser> Users { get; } = new();

    // Mesmo contrato do repositório real: não sobrescreve se já existir.
    public Task UpsertAsync(NotificationUser user, CancellationToken ct = default)
    {
        Users.TryAdd(user.Id, user);
        return Task.CompletedTask;
    }

    public Task<NotificationUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        Users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }
}
