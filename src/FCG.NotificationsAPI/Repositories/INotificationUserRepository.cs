using FCG.NotificationsAPI.Entities;

namespace FCG.NotificationsAPI.Repositories;

public interface INotificationUserRepository
{
    Task UpsertAsync(NotificationUser user, CancellationToken ct = default);
    Task<NotificationUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
