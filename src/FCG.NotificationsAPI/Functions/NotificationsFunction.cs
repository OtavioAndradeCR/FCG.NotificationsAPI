using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using FCG.NotificationsAPI.Handlers;
using FCG.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace FCG.NotificationsAPI.Functions;

public class NotificationsFunction
{
    private const string UserCreatedEventType      = "UserCreatedEvent";
    private const string PaymentProcessedEventType = "PaymentProcessedEvent";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceProvider _services;

    /// <summary>Construtor exigido pelo runtime do Lambda.</summary>
    public NotificationsFunction() : this(Program.ServiceProvider) { }

    /// <summary>Usado pelos testes para injetar fakes.</summary>
    public NotificationsFunction(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Entry point do SQS. Retorna SQSBatchResponse com APENAS as mensagens que falharam.
    /// Requer ReportBatchItemFailures no event source mapping (o workflow já configura).
    /// Lançar exceção aqui reentregaria o lote inteiro e duplicaria e-mails já enviados.
    /// </summary>
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        var logger = _services.GetRequiredService<ILogger<NotificationsFunction>>();

        logger.LogInformation("[Lambda] Invocação recebida com {Count} mensagem(ns).", sqsEvent.Records.Count);

        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessRecordAsync(record, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Lambda] Falha ao processar MessageId={MessageId}", record.MessageId);
                failures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = record.MessageId });
            }
        }

        return new SQSBatchResponse { BatchItemFailures = failures };
    }

    private async Task ProcessRecordAsync(SQSEvent.SQSMessage record, ILogger logger)
    {
        var eventType = GetEventType(record);

        logger.LogInformation(
            "[Lambda] Processando MessageId={MessageId} | EventType={EventType}",
            record.MessageId, eventType);

        using var scope = _services.CreateScope();

        switch (eventType)
        {
            case UserCreatedEventType:
            {
                var evt = JsonSerializer.Deserialize<UserCreatedEvent>(record.Body, JsonOptions)
                    ?? throw new InvalidOperationException("Falha ao desserializar UserCreatedEvent.");
                await scope.ServiceProvider.GetRequiredService<UserCreatedHandler>().HandleAsync(evt);
                break;
            }

            case PaymentProcessedEventType:
            {
                var evt = JsonSerializer.Deserialize<PaymentProcessedEvent>(record.Body, JsonOptions)
                    ?? throw new InvalidOperationException("Falha ao desserializar PaymentProcessedEvent.");
                await scope.ServiceProvider.GetRequiredService<PaymentProcessedHandler>().HandleAsync(evt);
                break;
            }

            default:
                // Antes: logava warning e DESCARTAVA a mensagem (perda silenciosa).
                // Agora: falha, e a DLQ da fila guarda a mensagem para análise.
                throw new InvalidOperationException($"EventType desconhecido: '{eventType}'.");
        }
    }

    private static string GetEventType(SQSEvent.SQSMessage record)
    {
        if (record.MessageAttributes is not null &&
            record.MessageAttributes.TryGetValue("EventType", out var attr) &&
            !string.IsNullOrWhiteSpace(attr.StringValue))
        {
            return attr.StringValue;
        }

        var sourceArn = record.EventSourceArn ?? string.Empty;

        if (sourceArn.Contains("user-created", StringComparison.OrdinalIgnoreCase))
            return UserCreatedEventType;

        if (sourceArn.Contains("payment-processed", StringComparison.OrdinalIgnoreCase))
            return PaymentProcessedEventType;

        return string.Empty;
    }
}
