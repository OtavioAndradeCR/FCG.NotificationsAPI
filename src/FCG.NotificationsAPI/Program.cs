using FCG.NotificationsAPI.Data;
using FCG.NotificationsAPI.Extensions;
using FCG.NotificationsAPI.Handlers;
using FCG.NotificationsAPI.Repositories;
using FCG.NotificationsAPI.Services;
using FCG.NotificationsAPI.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.NotificationsAPI;

/// <summary>
/// Inicializa o container de DI uma única vez por instância Lambda (warm start).
/// Invocações subsequentes reutilizam o mesmo ServiceProvider.
/// </summary>
public static class Program
{
    private static IServiceProvider? _serviceProvider;
    private static readonly object _lock = new();

    public static IServiceProvider ServiceProvider
    {
        get
        {
            if (_serviceProvider is not null)
                return _serviceProvider;

            lock (_lock)
            {
                _serviceProvider ??= BuildServiceProvider();
            }

            return _serviceProvider;
        }
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();

        // Logging
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // Configuração tipada
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));

        // Banco de dados
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")),
            ServiceLifetime.Transient);

        // Repositório, serviços e handlers
        services.AddTransient<INotificationUserRepository, NotificationUserRepository>();
        services.AddTransient<IEmailNotificationService, EmailNotificationService>();
        services.AddHandlers();

        var provider = services.BuildServiceProvider();

        // Aplicar migrations na inicialização fria do Lambda
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        return provider;
    }
}
