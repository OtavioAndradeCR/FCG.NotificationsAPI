using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using FCG.NotificationsAPI.Handlers;
using FCG.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Registra o serializador padrão da Lambda
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace FCG.NotificationsAPI.Functions;

public class NotificationsFunction
{
    // Identificadores do tipo de evento — devem bater com o atributo
    // MessageGroupId ou MessageAttribute configurado no Shovel/publisher
    private const string UserCreatedEventType     = "UserCreatedEvent";
    private const string PaymentProcessedEventType = "PaymentProcessedEvent";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Entry point triggerado pelo SQS.
    /// Cada mensagem SQS carrega um envelope com "EventType" e "Payload".
    /// </summary>
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        var logger = Program.ServiceProvider
            .GetRequiredService<ILogger<NotificationsFunction>>();

        logger.LogInformation(
            "[Lambda] Invocação recebida com {Count} mensagem(ns).",
            sqsEvent.Records.Count);

        // Lambda com SQS: processar cada mensagem individualmente
        // e reportar falhas parciais para não bloquear mensagens válidas
        var batchItemFailures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessRecordAsync(record, context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[Lambda] Falha ao processar mensagem MessageId={MessageId}",
                    record.MessageId);

                // Reporta falha parcial — SQS vai retentar só essa mensagem
                batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = record.MessageId
                });
            }
        }

        if (batchItemFailures.Count > 0)
        {
            throw new Exception(
                $"{batchItemFailures.Count} mensagem(ns) falharam. " +
                $"IDs: {string.Join(", ", batchItemFailures.Select(f => f.ItemIdentifier))}");
        }
    }

    private static async Task ProcessRecordAsync(SQSEvent.SQSMessage record, ILambdaContext context)
    {
        var logger = Program.ServiceProvider
            .GetRequiredService<ILogger<NotificationsFunction>>();

        // Extrair tipo do evento do atributo da mensagem SQS
        // O Shovel configura esse atributo via "x-event-type" ou similar
        var eventType = GetEventType(record);

        logger.LogInformation(
            "[Lambda] Processando MessageId={MessageId} | EventType={EventType}",
            record.MessageId, eventType);

        using var scope = Program.ServiceProvider.CreateScope();

        switch (eventType)
        {
            case UserCreatedEventType:
            {
                var evt = JsonSerializer.Deserialize<UserCreatedEvent>(record.Body, JsonOptions)
                    ?? throw new InvalidOperationException("Falha ao desserializar UserCreatedEvent.");

                var handler = scope.ServiceProvider.GetRequiredService<UserCreatedHandler>();
                await handler.HandleAsync(evt);
                break;
            }

            case PaymentProcessedEventType:
            {
                var evt = JsonSerializer.Deserialize<PaymentProcessedEvent>(record.Body, JsonOptions)
                    ?? throw new InvalidOperationException("Falha ao desserializar PaymentProcessedEvent.");

                var handler = scope.ServiceProvider.GetRequiredService<PaymentProcessedHandler>();
                await handler.HandleAsync(evt);
                break;
            }

            default:
                logger.LogWarning(
                    "[Lambda] EventType desconhecido: {EventType}. Mensagem ignorada.",
                    eventType);
                break;
        }
    }

    private static string GetEventType(SQSEvent.SQSMessage record)
    {
        // Primeiro tenta pegar do atributo da mensagem (configurado pelo Shovel)
        if (record.MessageAttributes.TryGetValue("EventType", out var attr))
            return attr.StringValue ?? string.Empty;

        // Fallback: tenta inferir pelo nome da fila de origem
        var sourceArn = record.EventSourceArn ?? string.Empty;

        if (sourceArn.Contains("user-created", StringComparison.OrdinalIgnoreCase))
            return UserCreatedEventType;

        if (sourceArn.Contains("payment-processed", StringComparison.OrdinalIgnoreCase))
            return PaymentProcessedEventType;

        return string.Empty;
    }
}
