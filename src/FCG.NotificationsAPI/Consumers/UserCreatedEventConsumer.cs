using FCG.NotificationsAPI.Entities;
using FCG.NotificationsAPI.Repositories;
using FCG.NotificationsAPI.Services;
using FCG.Shared.Events;
using MassTransit;

namespace FCG.NotificationsAPI.Consumers;

public class UserCreatedEventConsumer : IConsumer<UserCreatedEvent>
{
    private readonly IEmailNotificationService _emailService;
    private readonly INotificationUserRepository _userRepository;
    private readonly ILogger<UserCreatedEventConsumer> _logger;

    public UserCreatedEventConsumer(
        IEmailNotificationService emailService,
        INotificationUserRepository userRepository,
        ILogger<UserCreatedEventConsumer> logger)
    {
        _emailService = emailService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
{
    var evt = context.Message;

    _logger.LogInformation(
        "[UserCreatedEvent] Recebido | UserId={UserId} | Nome={Name} | IsAdmin={IsAdmin}",
        evt.UserId, evt.Name, evt.IsAdmin);

    var user = new NotificationUser
    {
        Id = evt.UserId,
        Name = evt.Name,
        Email = evt.Email,
        IsAdmin = evt.IsAdmin,
        CreatedAt = DateTime.UtcNow
    };

    await _userRepository.UpsertAsync(user, context.CancellationToken);

    _logger.LogInformation(
        "[UserCreatedEvent] Usuário persistido localmente | UserId={UserId}",
        evt.UserId);

    if (evt.IsAdmin)
    {
        if (string.IsNullOrWhiteSpace(evt.TemporaryPassword))
        {
            _logger.LogWarning(
                "[UserCreatedEvent] Admin sem senha temporária. Nenhum e-mail enviado. UserId={UserId}",
                evt.UserId);

            return;
        }

        await _emailService.SendWelcomeAdminAsync(
            evt.Name,
            evt.Email,
            evt.TemporaryPassword);
    }
    else
    {
        await _emailService.SendWelcomeAsync(
            evt.Name,
            evt.Email);
    }
}
}
