using FCG.NotificationsAPI.Handlers;
using FCG.Shared.Events;
using Microsoft.Extensions.DependencyInjection;

// ─────────────────────────────────────────────────────────────
// Teste ponta a ponta: executa o código REAL da Lambda
// (DI + handlers + repositório DynamoDB + envio SMTP)
// ─────────────────────────────────────────────────────────────

var userId = Guid.NewGuid();
var email = "grupofiap77@gmail.com"; // inbox do usuário para verificação

Console.WriteLine("=== TESTE E2E: UserCreatedEvent (usuário comum) ===");
var userHandler = FCG.NotificationsAPI.Program.ServiceProvider.GetRequiredService<UserCreatedHandler>();
await userHandler.HandleAsync(new UserCreatedEvent(
    UserId: userId,
    Name: "Teste E2E DynamoDB",
    Email: email,
    IsAdmin: false,
    TemporaryPassword: null));

Console.WriteLine();
Console.WriteLine("=== TESTE E2E: PaymentProcessedEvent (Approved) ===");
var paymentHandler = FCG.NotificationsAPI.Program.ServiceProvider.GetRequiredService<PaymentProcessedHandler>();
await paymentHandler.HandleAsync(new PaymentProcessedEvent(
    OrderId: Guid.NewGuid(),
    UserId: userId,
    GameId: Guid.NewGuid(),
    GameTitle: "Test Game E2E",
    Price: 99.90m,
    Status: "Approved"));

Console.WriteLine();
Console.WriteLine("=== E2E OK: usuário persistido no DynamoDB e e-mails enviados ===");
Console.WriteLine($"UserId: {userId}");