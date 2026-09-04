using FCG.NotificationsAPI.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.NotificationsAPI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHandlers(this IServiceCollection services)
    {
        services.AddTransient<UserCreatedHandler>();
        services.AddTransient<PaymentProcessedHandler>();
        return services;
    }
}
