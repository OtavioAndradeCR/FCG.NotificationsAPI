using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FCG.NotificationsAPI.Data;
using FCG.NotificationsAPI.Entities;
using FCG.NotificationsAPI.Settings;
using Microsoft.Extensions.Options;

namespace FCG.NotificationsAPI.Repositories;

public class NotificationUserRepository : INotificationUserRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public NotificationUserRepository(
        IAmazonDynamoDB dynamoDb,
        IOptions<DynamoDbSettings> settings)
    {
        _dynamoDb = dynamoDb;
        _tableName = settings.Value.TableName;
    }

    /// <summary>
    /// Insere o usuário caso ainda não exista (idempotente —
    /// o mesmo evento pode chegar mais de uma vez em caso de redelivery).
    /// Usa PutItem condicional: se o Id já existe, não sobrescreve.
    /// </summary>
    public async Task UpsertAsync(NotificationUser user, CancellationToken ct = default)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [DynamoDbConstants.AttrId] = new() { S = user.Id.ToString() },
            [DynamoDbConstants.AttrName] = new() { S = user.Name },
            [DynamoDbConstants.AttrEmail] = new() { S = user.Email },
            [DynamoDbConstants.AttrIsAdmin] = new() { BOOL = user.IsAdmin },
            [DynamoDbConstants.AttrCreatedAt] = new() { S = user.CreatedAt.ToString("O") }
        };

        var request = new PutItemRequest
        {
            TableName = _tableName,
            Item = item,
            // Idempotente: só insere se o Id ainda não existir
            ConditionExpression = $"attribute_not_exists({DynamoDbConstants.AttrId})"
        };

        try
        {
            await _dynamoDb.PutItemAsync(request, ct);
        }
        catch (ConditionalCheckFailedException)
        {
            // Item já existe — comportamento idempotente preservado (não sobrescreve)
        }
    }

    public async Task<NotificationUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [DynamoDbConstants.AttrId] = new() { S = id.ToString() }
            },
            // Leitura consistente: garante que um usuário recém-persistido
            // seja encontrado imediatamente (importante no fluxo UserCreated → PaymentProcessed)
            ConsistentRead = true
        };

        var response = await _dynamoDb.GetItemAsync(request, ct);

        if (!response.IsItemSet)
            return null;

        return new NotificationUser
        {
            Id = Guid.Parse(response.Item[DynamoDbConstants.AttrId].S),
            Name = response.Item[DynamoDbConstants.AttrName].S,
            Email = response.Item[DynamoDbConstants.AttrEmail].S,
            IsAdmin = response.Item[DynamoDbConstants.AttrIsAdmin].BOOL ?? false,
            CreatedAt = DateTime.Parse(
                response.Item[DynamoDbConstants.AttrCreatedAt].S,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind)
        };
    }
}