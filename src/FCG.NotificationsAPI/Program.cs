using Amazon.DynamoDBv2;
using FCG.NotificationsAPI.Extensions;
using FCG.NotificationsAPI.Handlers;
using FCG.NotificationsAPI.Repositories;
using FCG.NotificationsAPI.Services;
using FCG.NotificationsAPI.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        services.Configure<DynamoDbSettings>(configuration.GetSection(DynamoDbSettings.SectionName));

        // DynamoDB — credenciais resolvidas pela default credential chain do SDK:
        //   - Lambda: execution role (sem credenciais explícitas)
        //   - Local: variáveis de ambiente AWS_ACCESS_KEY_ID/AWS_SECRET_ACCESS_KEY/AWS_SESSION_TOKEN
        //            ou ~/.aws/credentials (aws configure)
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<DynamoDbSettings>>().Value;

            var config = new AmazonDynamoDBConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(
                    configuration["AWS_REGION"] ?? "us-east-1")
            };

            // DynamoDB Local (dev offline) — aponta para localhost
            if (!string.IsNullOrEmpty(settings.ServiceURL))
                config.ServiceURL = settings.ServiceURL;

            return new AmazonDynamoDBClient(config);
        });

        // Repositório, serviços e handlers
        services.AddTransient<INotificationUserRepository, NotificationUserRepository>();
        services.AddTransient<IEmailNotificationService, EmailNotificationService>();
        services.AddHandlers();

        return services.BuildServiceProvider();
    }
}