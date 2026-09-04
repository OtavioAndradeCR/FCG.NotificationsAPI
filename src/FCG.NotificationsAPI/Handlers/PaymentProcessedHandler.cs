using FCG.NotificationsAPI.Repositories;
using FCG.NotificationsAPI.Services;
using FCG.Shared.Events;
using Microsoft.Extensions.Logging;

namespace FCG.NotificationsAPI.Handlers;

public class PaymentProcessedHandler
{
    private readonly IEmailNotificationService _emailService;
    private readonly INotificationUserRepository _userRepository;
    private readonly ILogger<PaymentProcessedHandler> _logger;

    public PaymentProcessedHandler(
        IEmailNotificationService emailService,
        INotificationUserRepository userRepository,
        ILogger<PaymentProcessedHandler> logger)
    {
        _emailService = emailService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task HandleAsync(PaymentProcessedEvent evt, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[PaymentProcessedEvent] Recebido | OrderId={OrderId} | UserId={UserId} | Status={Status}",
            evt.OrderId, evt.UserId, evt.Status);

        if (!string.Equals(evt.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "[PaymentProcessedEvent] Status={Status}. Nenhum e-mail enviado.", evt.Status);
            return;
        }

        var user = await _userRepository.GetByIdAsync(evt.UserId, ct);

        if (user is null)
        {
            // Lança exceção — o SQS vai retentar automaticamente
            throw new InvalidOperationException(
                $"[PaymentProcessedEvent] Usuário {evt.UserId} não encontrado. " +
                "UserCreatedEvent pode não ter sido processado ainda.");
        }

        await _emailService.SendPurchaseConfirmedAsync(
            user.Name,
            user.Email,
            evt.GameTitle,
            evt.Price,
            evt.OrderId);
    }
}
