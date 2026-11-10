using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using FCG.NotificationsAPI.Entities;
using FCG.NotificationsAPI.Extensions;
using FCG.NotificationsAPI.Functions;
using FCG.NotificationsAPI.Repositories;
using FCG.NotificationsAPI.Services;
using FCG.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FCG.NotificationsAPI.Tests;

public class NotificationsFunctionTests
{
    private readonly FakeEmailService _email = new();
    private readonly FakeUserRepository _repo = new();
    private readonly NotificationsFunction _function;

    public NotificationsFunctionTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IEmailNotificationService>(_email);
        services.AddSingleton<INotificationUserRepository>(_repo);
        services.AddHandlers();

        _function = new NotificationsFunction(services.BuildServiceProvider());
    }

    private static SQSEvent.SQSMessage Message(string id, string body, string? eventType, string arn = "arn:aws:sqs:us-east-1:111111111111:outra-fila")
    {
        var msg = new SQSEvent.SQSMessage
        {
            MessageId = id,
            Body = body,
            EventSourceArn = arn,
            MessageAttributes = new Dictionary<string, SQSEvent.MessageAttribute>()
        };

        if (eventType is not null)
            msg.MessageAttributes["EventType"] = new SQSEvent.MessageAttribute { DataType = "String", StringValue = eventType };

        return msg;
    }

    private static SQSEvent Batch(params SQSEvent.SQSMessage[] messages) =>
        new() { Records = messages.ToList() };

    private static string UserJson(Guid id) =>
        JsonSerializer.Serialize(new UserCreatedEvent(id, "Eva", "eva@teste.com", false, null));

    private static string PaymentJson(Guid userId, string status = "Approved") =>
        JsonSerializer.Serialize(new PaymentProcessedEvent(Guid.NewGuid(), userId, Guid.NewGuid(), "Hades", 79.90m, status));

    [Fact]
    public async Task LoteComUmaFalha_ReportaSomenteAFalha_ENaoReprocessaOsSucessos()
    {
        // Regressão: antes a função lançava exceção e o SQS reentregava o lote inteiro,
        // reenviando e-mails que já tinham saído.
        var okUser = Guid.NewGuid();
        var batch = Batch(
            Message("ok-1", UserJson(okUser), "UserCreatedEvent"),
            Message("falha-1", PaymentJson(Guid.NewGuid()), "PaymentProcessedEvent")); // usuário não existe

        var response = await _function.FunctionHandler(batch, null!);

        var failure = Assert.Single(response.BatchItemFailures);
        Assert.Equal("falha-1", failure.ItemIdentifier);
        Assert.Single(_email.Welcome);
    }

    [Fact]
    public async Task LoteTotalmenteValido_NaoReportaFalhas()
    {
        var userId = Guid.NewGuid();
        var batch = Batch(
            Message("1", UserJson(userId), "UserCreatedEvent"),
            Message("2", PaymentJson(userId), "PaymentProcessedEvent"));

        var response = await _function.FunctionHandler(batch, null!);

        Assert.Empty(response.BatchItemFailures);
        Assert.Single(_email.Welcome);
        Assert.Single(_email.Purchases);
    }

    [Fact]
    public async Task EventTypeDesconhecido_EhReportadoComoFalha()
    {
        var response = await _function.FunctionHandler(
            Batch(Message("x", "{}", "TipoQueNaoExiste")), null!);

        Assert.Equal("x", Assert.Single(response.BatchItemFailures).ItemIdentifier);
    }

    [Fact]
    public async Task JsonInvalido_EhReportadoComoFalha()
    {
        var response = await _function.FunctionHandler(
            Batch(Message("bad", "isto-nao-e-json", "UserCreatedEvent")), null!);

        Assert.Equal("bad", Assert.Single(response.BatchItemFailures).ItemIdentifier);
    }

    [Fact]
    public async Task SemAtributoEventType_InfereTipoPeloNomeDaFila()
    {
        var response = await _function.FunctionHandler(
            Batch(Message("1", UserJson(Guid.NewGuid()), null,
                "arn:aws:sqs:us-east-1:111111111111:fcg-user-created")), null!);

        Assert.Empty(response.BatchItemFailures);
        Assert.Single(_email.Welcome);
    }
}
