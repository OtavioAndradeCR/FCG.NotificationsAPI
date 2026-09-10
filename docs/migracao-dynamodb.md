# Plano de Implementação — Migração PostgreSQL → DynamoDB

> **Projeto:** FCG.NotificationsAPI (AWS Lambda)
> **Objetivo:** Trocar o banco de dados de PostgreSQL/EF Core para DynamoDB, **preservando exatamente o fluxo atual** de notificação por e-mail.
> **Status:** Aprovado — aguardando implementação

---

## Contexto atual (fluxo preservado)

```
SQS (UserCreatedEvent / PaymentProcessedEvent)
   └─► NotificationsFunction (Lambda)
         ├─► UserCreatedHandler
         │     ├─► INotificationUserRepository.UpsertAsync()  → DynamoDB (novo)
         │     └─► IEmailNotificationService (welcome/admin)
         └─► PaymentProcessedHandler
               ├─► INotificationUserRepository.GetByIdAsync() → DynamoDB (novo)
               └─► IEmailNotificationService (purchase confirmed)
```

**Regra de ouro:** `Handlers/`, `Functions/`, `Services/`, `Entities/` e a interface `INotificationUserRepository` **não mudam**. Só a camada de dados e a configuração.

**Pontos de contato com o banco hoje:**

| Arquivo | Papel |
|---|---|
| `Program.cs` | `AddDbContext` + `db.Database.Migrate()` no cold start |
| `Data/ApplicationDbContext.cs` | DbContext EF Core |
| `Data/Configurations/NotificationUserConfiguration.cs` | Mapeamento da tabela |
| `Repositories/NotificationUserRepository.cs` | `UpsertAsync` (idempotente) + `GetByIdAsync` |
| `Migrations/` | Schema SQL |
| `appsettings.json` / `.Development.json` | `ConnectionStrings__DefaultConnection` |
| `k8s/` | Secret `ConnectionStrings__DefaultConnection` |

---

## Fase 1 — Dependências (csproj)

**Remover:**
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.2
- `Microsoft.EntityFrameworkCore` 10.0.9
- `Microsoft.EntityFrameworkCore.Design` 10.0.9

**Adicionar:**
- `AWSSDK.DynamoDBv2` **4.0.103.7** (AWS SDK for .NET V4 — a linha 3.7.x foi substituída)

> `Amazon.Lambda.Core`, `Amazon.Lambda.SQSEvents` e o serializador já estão presentes e continuam.

---

## Fase 2 — Modelo de dados DynamoDB

**Tabela:** `fcg-notification-users` (já criada — partition key `Id`)

| Chave | Tipo | Valor |
|---|---|---|
| **Partition Key** | `Id` | String (Guid serializado) |

**Atributos:**

| Atributo | Tipo DynamoDB | Origem |
|---|---|---|
| `Name` | String | `NotificationUser.Name` |
| `Email` | String | `NotificationUser.Email` |
| `IsAdmin` | Boolean | `NotificationUser.IsAdmin` |
| `CreatedAt` | String (ISO 8601 UTC) | `NotificationUser.CreatedAt` |

**GSI opcional:** `EmailIndex` em `Email` — paridade com o índice atual do Postgres (o código não consulta por Email hoje, mas mantém a capacidade futura).

> **Por que String para `Id` e `CreatedAt`?** DynamoDB não tem tipo `Guid`/`DateTime` nativo. String ISO-8601 mantém ordenação cronológica legível e evita conversões binárias.

---

## Fase 3 — Camada de dados

**Deletar:**
- `Data/ApplicationDbContext.cs`
- `Data/Configurations/NotificationUserConfiguration.cs`
- `Migrations/` (pasta inteira — DynamoDB é schemaless)

**Reescrever `Repositories/NotificationUserRepository.cs`** — a interface `INotificationUserRepository` **NÃO muda** (handlers intactos):

| Operação atual (EF Core) | Nova operação (DynamoDB) | Comportamento preservado |
|---|---|---|
| `AnyAsync` + `AddAsync` + `SaveChanges` | `PutItem` com `ConditionExpression: attribute_not_exists(Id)` | ✅ Idempotente — só insere se não existir; se existir, **não sobrescreve** |
| `FirstOrDefaultAsync(u => u.Id == id)` | `GetItem` por PK (`ConsistentRead: true`) | ✅ Retorna `null` se não existir |

**Criar:**
- `Data/DynamoDbConstants.cs` — centraliza nome da tabela e atributos (evita magic strings)
- `Settings/DynamoDbSettings.cs` — `TableName` + `ServiceURL` (padrão `IOptions`, igual ao `SmtpSettings`)

---

## Fase 4 — DI / Program.cs

**Remover:**
```csharp
services.AddDbContext<ApplicationDbContext>(...);
...
using var scope = provider.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
db.Database.Migrate();   // ← não existe migration em DynamoDB
```

**Adicionar:**
```csharp
services.Configure<DynamoDbSettings>(configuration.GetSection(DynamoDbSettings.SectionName));

services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<DynamoDbSettings>>().Value;
    var config = new AmazonDynamoDBConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(
            configuration["AWS_REGION"] ?? "us-east-1")
    };
    // DynamoDB Local (dev offline) — aponta para localhost
    if (!string.IsNullOrEmpty(settings.ServiceURL))
        config.ServiceURL = settings.ServiceURL;
    return new AmazonDynamoDBClient(config);
});

services.AddTransient<INotificationUserRepository, NotificationUserRepository>();
```

> ⚠️ **Nenhuma configuração de credenciais no código.** O SDK usa a *default credential chain* — é isso que permite o session token do Lab funcionar sem alteração de código.

---

## Fase 5 — Configuração (inclui credenciais do Lab)

### 5.1 `appsettings.json`

```jsonc
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "DynamoDB": {
    "TableName": "fcg-notification-users",
    "ServiceURL": ""    // preenchido apenas para DynamoDB Local
  },
  "Smtp": { ... }       // inalterado
}
```

### 5.2 `appsettings.Development.json`

Remover `ConnectionStrings`; adicionar `DynamoDB:ServiceURL: http://localhost:8000` (DynamoDB Local).

### 5.3 `.env` — credenciais do AWS Lab (CRÍTICO)

O Lab fornece **credenciais temporárias (STS)** — access key + secret + **session token**. O `.env` atual só tem as duas primeiras:

```bash
# ── AWS (credenciais temporárias do AWS Lab) ──
AWS_ACCESS_KEY_ID=<do lab>
AWS_SECRET_ACCESS_KEY=<do lab>
AWS_SESSION_TOKEN=<do lab>          # ← ADICIONAR (obrigatório p/ DynamoDB real)
AWS_REGION=us-east-1

# DynamoDB
DYNAMODB_TABLE_NAME=fcg-notification-users
```

**Como o SDK resolve as credenciais (sem mudança de código):**

| Fonte | Como funciona |
|---|---|
| Variáveis de ambiente | SDK lê `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY` + `AWS_SESSION_TOKEN` automaticamente |
| `~/.aws/credentials` | Rodar `aws configure` no Lab grava os 3 campos; SDK lê o profile `default` |
| Lambda na AWS | Usa a **execution role** — ignora tudo acima |

**Regra prática:**

| Cenário | Session token? |
|---|---|
| Lambda em produção | ❌ Não — execution role |
| Dev local → DynamoDB real (Lab) | ✅ **Sim — obrigatório** |
| Dev local → DynamoDB Local | ❌ Não — aceita qualquer credencial |

> Sem o `AWS_SESSION_TOKEN`, chamadas ao DynamoDB real falham com `The security token included in the request is invalid`.

### 5.4 Variáveis de ambiente da Lambda

| Antes | Depois |
|---|---|
| `ConnectionStrings__DefaultConnection` | `DynamoDB__TableName` |
| — | `AWS_REGION` (já usada) |

---

## Fase 6 — Infraestrutura

### 6.1 IAM (Lambda)

```json
{
  "Effect": "Allow",
  "Action": ["dynamodb:GetItem", "dynamodb:PutItem"],
  "Resource": "arn:aws:dynamodb:*:*:table/fcg-notification-users"
}
```

### 6.2 `k8s/`

Trocar `ConnectionStrings__DefaultConnection` por `DynamoDB__TableName` em:
- `k8s/configmap.yaml`
- `k8s/secret.yaml`
- `k8s/deployment.yaml`

### 6.3 `docker-compose.yml`

- ⚠️ **NÃO remover o Postgres** — `users`, `catalog` e `payments` ainda dependem dele
- ➕ Adicionar `dynamodb-local` (imagem `amazon/dynamodb-local`, porta 8000) para dev offline

### 6.4 `README.md` — instruções atualizadas

1. **Credenciais do Lab:** `aws configure` (preenche access key + secret + session token) **ou** exportar as 3 variáveis no `.env`
2. Criar a tabela (já criada — documentar para reprodutibilidade):

```bash
aws dynamodb create-table \
  --table-name fcg-notification-users \
  --attribute-definitions AttributeName=Id,AttributeType=S \
  --key-schema AttributeName=Id,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1
```

3. Deploy Lambda + variáveis de ambiente (`DynamoDB__TableName`, `AWS_REGION`)
4. Teste local: `docker compose up dynamodb-local` + `DynamoDB:ServiceURL=http://localhost:8000`

---

## Fase 7 — Validação

1. `dotnet build` — sem erros
2. Teste local com DynamoDB Local (sem gastar conta do Lab)
3. Teste contra DynamoDB real do Lab (com `AWS_SESSION_TOKEN` no ambiente)
4. Idempotência: mesmo `UserCreatedEvent` 2x → 1 item na tabela
5. `PaymentProcessedHandler`: usuário inexistente → exceção → retry SQS (preservado)
6. Deploy Lambda + atualizar env vars

---

## Arquivos afetados (resumo)

| Ação | Arquivo |
|---|---|
| ✏️ Editar | `FCG.NotificationsAPI.csproj` |
| ✏️ Editar | `Program.cs` |
| ✏️ Reescrever | `Repositories/NotificationUserRepository.cs` |
| ✏️ Editar | `appsettings.json`, `appsettings.Development.json` |
| ✏️ Editar | `.env` (adicionar `AWS_SESSION_TOKEN` + `DYNAMODB_TABLE_NAME`) |
| ✏️ Editar | `k8s/configmap.yaml`, `k8s/secret.yaml`, `k8s/deployment.yaml` |
| ✏️ Editar | `README.md`, `docker-compose.yml` |
| ➕ Criar | `Data/DynamoDbConstants.cs`, `Settings/DynamoDbSettings.cs` |
| 🗑️ Deletar | `Data/ApplicationDbContext.cs`, `Data/Configurations/`, `Migrations/` |
| ✅ Intactos | `Handlers/`, `Functions/`, `Services/`, `Entities/`, `INotificationUserRepository.cs` |

---

## Riscos e observações

- **Idempotência**: `PutItem` condicional (`attribute_not_exists`) é o ponto crítico — sem ele, redelivery do SQS sobrescreveria o registro
- **Session token**: só é necessário em dev local contra DynamoDB real; Lambda usa execution role
- **Custo**: on-demand (`PAY_PER_REQUEST`) é suficiente para esse volume
- **Consistência**: `GetItem` default é *eventually consistent*; usar `ConsistentRead: true` se precisar de leitura imediata pós-escrita
- **SDK V4**: coleções em respostas agora podem vir `null` (ex.: `response.Item`) — usar `IsItemSet` em vez de checar `Count`