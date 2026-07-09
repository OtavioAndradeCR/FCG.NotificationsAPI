using FCG.NotificationsAPI.Data;
using FCG.NotificationsAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace FCG.NotificationsAPI.Repositories;

public class NotificationUserRepository : INotificationUserRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationUserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Insere o usuário caso ainda não exista (idempotente —
    /// o mesmo evento pode chegar mais de uma vez em caso de redelivery).
    /// </summary>
    public async Task UpsertAsync(NotificationUser user, CancellationToken ct = default)
    {
        var exists = await _context.NotificationUsers
            .AnyAsync(u => u.Id == user.Id, ct);

        if (!exists)
        {
            await _context.NotificationUsers.AddAsync(user, ct);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<NotificationUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.NotificationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }
}
