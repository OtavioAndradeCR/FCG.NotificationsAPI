using FCG.NotificationsAPI.Consumers;
using FCG.NotificationsAPI.Data;
using FCG.NotificationsAPI.Repositories;
using FCG.NotificationsAPI.Services;
using FCG.NotificationsAPI.Settings;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FCG.NotificationsAPI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();
        services.AddScoped<INotificationUserRepository, NotificationUserRepository>();
        return services;
    }

    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        return services;
    }

    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var host = configuration["RabbitMQ:Host"] ?? "rabbitmq";
        var user = configuration["RabbitMQ:Username"] ?? "guest";
        var pass = configuration["RabbitMQ:Password"] ?? "guest";

        services.AddMassTransit(x =>
        {
            x.AddConsumer<UserCreatedEventConsumer>();
            x.AddConsumer<PaymentProcessedEventConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(host, "/", h =>
                {
                    h.Username(user);
                    h.Password(pass);
                });

                cfg.ReceiveEndpoint(
                    configuration["RabbitMQ:Queues:UserCreated"] ?? "fcg.user-created",
                    e =>
                    {
                        e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                        e.ConfigureConsumer<UserCreatedEventConsumer>(ctx);
                    });

                cfg.ReceiveEndpoint(
                    configuration["RabbitMQ:Queues:PaymentProcessed"] ?? "fcg.payment-processed",
                    e =>
                    {
                        e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(10)));
                        e.ConfigureConsumer<PaymentProcessedEventConsumer>(ctx);
                    });
            });
        });

        return services;
    }
}
