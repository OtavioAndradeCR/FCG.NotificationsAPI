using FCG.NotificationsAPI.Entities;
using FCG.NotificationsAPI.Handlers;
using FCG.Shared.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FCG.NotificationsAPI.Tests;

public class UserCreatedHandlerTests
{
    private readonly FakeEmailService _email = new();
    private readonly FakeUserRepository _repo = new();

    private UserCreatedHandler Build() =>
        new(_email, _repo, NullLogger<UserCreatedHandler>.Instance);

    [Fact]
    public async Task UsuarioComum_PersisteEEnviaBoasVindas()
    {
        var id = Guid.NewGuid();

        await Build().HandleAsync(new UserCreatedEvent(id, "Ana", "ana@teste.com", false, null));

        Assert.True(_repo.Users.ContainsKey(id));
        Assert.Equal("ana@teste.com", _repo.Users[id].Email);
        Assert.Single(_email.Welcome);
        Assert.Empty(_email.Admin);
    }

    [Fact]
    public async Task Admin_EnviaEmailComSenhaTemporaria()
    {
        await Build().HandleAsync(
            new UserCreatedEvent(Guid.NewGuid(), "Bia", "bia@teste.com", true, "Temp#123"));

        var sent = Assert.Single(_email.Admin);
        Assert.Equal("Temp#123", sent.Password);
        Assert.Empty(_email.Welcome);
    }

    [Fact]
    public async Task Admin_SemSenha_NaoEnviaEmail()
    {
        await Build().HandleAsync(
            new UserCreatedEvent(Guid.NewGuid(), "Caio", "caio@teste.com", true, "  "));

        Assert.Equal(0, _email.Total);
    }
}

public class PaymentProcessedHandlerTests
{
    private readonly FakeEmailService _email = new();
    private readonly FakeUserRepository _repo = new();

    private PaymentProcessedHandler Build() =>
        new(_email, _repo, NullLogger<PaymentProcessedHandler>.Instance);

    private static PaymentProcessedEvent Payment(Guid userId, string status) =>
        new(Guid.NewGuid(), userId, Guid.NewGuid(), "Zelda", 199.90m, status);

    private void SeedUser(Guid id) =>
        _repo.Users[id] = new NotificationUser { Id = id, Name = "Dani", Email = "dani@teste.com" };

    [Theory]
    [InlineData("Approved")]
    [InlineData("approved")]
    public async Task Aprovado_EnviaConfirmacaoComDadosDoUsuarioDoBanco(string status)
    {
        var id = Guid.NewGuid();
        SeedUser(id);

        await Build().HandleAsync(Payment(id, status));

        var sent = Assert.Single(_email.Purchases);
        Assert.Equal("Dani", sent.Name);
        Assert.Equal("dani@teste.com", sent.Email);
        Assert.Equal("Zelda", sent.Game);
        Assert.Equal(199.90m, sent.Price);
    }

    [Fact]
    public async Task Rejeitado_NaoEnviaEmail()
    {
        var id = Guid.NewGuid();
        SeedUser(id);

        await Build().HandleAsync(Payment(id, "Rejected"));

        Assert.Equal(0, _email.Total);
    }

    [Fact]
    public async Task Aprovado_UsuarioInexistente_LancaExcecaoParaSqsRetentar()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build().HandleAsync(Payment(Guid.NewGuid(), "Approved")));

        Assert.Equal(0, _email.Total);
    }
}
