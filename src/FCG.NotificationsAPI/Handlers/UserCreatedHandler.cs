using FCG.NotificationsAPI.Entities;
using FCG.NotificationsAPI.Repositories;
using FCG.NotificationsAPI.Services;
using FCG.Shared.Events;
using Microsoft.Extensions.Logging;

namespace FCG.NotificationsAPI.Handlers;

public class UserCreatedHandler
{
    private readonly IEmailNotificationService _emailService;
    private readonly INotificationUserRepository _userRepository;
    private readonly ILogger<UserCreatedHandler> _logger;

    public UserCreatedHandler(
        IEmailNotificationService emailService,
        INotificationUserRepository userRepository,
        ILogger<UserCreatedHandler> logger)
    {
        _emailService = emailService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task HandleAsync(UserCreatedEvent evt, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[UserCreatedEvent] Recebido | UserId={UserId} | Nome={Name} | IsAdmin={IsAdmin}",
            evt.UserId, evt.Name, evt.IsAdmin);

        // 1. Persistir espelho local para lookups futuros
        var user = new NotificationUser
        {
            Id        = evt.UserId,
            Name      = evt.Name,
            Email     = evt.Email,
            IsAdmin   = evt.IsAdmin,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.UpsertAsync(user, ct);

        _logger.LogInformation(
            "[UserCreatedEvent] Usuário persistido | UserId={UserId}", evt.UserId);

        // 2. Enviar e-mail adequado ao perfil
        if (evt.IsAdmin)
        {
            if (string.IsNullOrWhiteSpace(evt.TemporaryPassword))
            {
                _logger.LogWarning(
                    "[UserCreatedEvent] Admin sem senha temporária. E-mail não enviado. UserId={UserId}",
                    evt.UserId);
                return;
            }

            await _emailService.SendWelcomeAdminAsync(evt.Name, evt.Email, evt.TemporaryPassword);
        }
        else
        {
            await _emailService.SendWelcomeAsync(evt.Name, evt.Email);
        }
    }
}
