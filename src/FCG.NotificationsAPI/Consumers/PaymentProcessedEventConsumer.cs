using FCG.NotificationsAPI.Repositories;
using FCG.NotificationsAPI.Services;
using FCG.Shared.Events;
using MassTransit;

namespace FCG.NotificationsAPI.Consumers;

public class PaymentProcessedEventConsumer : IConsumer<PaymentProcessedEvent>
{
    private readonly IEmailNotificationService _emailService;
    private readonly INotificationUserRepository _userRepository;
    private readonly ILogger<PaymentProcessedEventConsumer> _logger;

    public PaymentProcessedEventConsumer(
        IEmailNotificationService emailService,
        INotificationUserRepository userRepository,
        ILogger<PaymentProcessedEventConsumer> logger)
    {
        _emailService = emailService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentProcessedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "[PaymentProcessedEvent] Recebido | OrderId={OrderId} | UserId={UserId} | Status={Status}",
            evt.OrderId, evt.UserId, evt.Status);

        if (!string.Equals(evt.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "[PaymentProcessedEvent] Pagamento não aprovado (Status={Status}). Nenhum e-mail enviado.",
                evt.Status);
            return;
        }

        // Buscar dados do usuário pelo UserId no banco local
        var user = await _userRepository.GetByIdAsync(evt.UserId, context.CancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "[PaymentProcessedEvent] Usuário não encontrado localmente. UserId={UserId}. " +
                "O UserCreatedEvent pode ainda não ter sido processado.",
                evt.UserId);
            // Lança exceção para o MassTransit retentar mais tarde
            throw new InvalidOperationException(
                $"Usuário {evt.UserId} não encontrado. Aguardando reprocessamento.");
        }

        await _emailService.SendPurchaseConfirmedAsync(
            user.Name,
            user.Email,
            evt.GameTitle,
            evt.Price,
            evt.OrderId);
    }
}
