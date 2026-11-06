# FCG.NotificationsAPI — AWS Lambda

Microsserviço de Notificações da plataforma **FIAP Cloud Games** refatorado para **AWS Lambda**, triggerado por filas **SQS**, persistindo dados no **DynamoDB** e enviando e-mails via **SMTP (Gmail)**.

---

## Arquitetura

```
UsersAPI ──────► RabbitMQ (fcg.user-created)   ─┐
PaymentsAPI ───► RabbitMQ (payment.processed)  ─┴─► NotificationsForwarder ──► SQS ──► Lambda (este repo)
```

O **FCG.NotificationsForwarder** (repositório próprio) é um worker MassTransit que consome as filas do RabbitMQ e republica cada mensagem no SQS, só confirmando (ack) no RabbitMQ depois que o SQS aceitou. O Lambda é acionado pelo SQS. Mensagens que falham 10 vezes vão para a DLQ `fcg-notifications-dlq`.

| Evento | Fluxo |
|---|---|
| `UserCreatedEvent` | Persiste o espelho do usuário no **DynamoDB** → envia e-mail de boas-vindas (ou admin com senha temporária) |
| `PaymentProcessedEvent` (Status=Approved) | Busca o usuário no **DynamoDB** → envia e-mail de compra confirmada |

> **Idempotência**: o `UpsertAsync` usa `PutItem` condicional (`attribute_not_exists(Id)`) — se o mesmo evento chegar de novo (redelivery SQS), o registro **não** é sobrescrito.

---

## Pré-requisitos

- .NET 10 SDK
- AWS CLI configurado (`aws configure`)
- Ferramenta dotnet-lambda: `dotnet tool install -g Amazon.Lambda.Tools`
- Conta AWS com permissões para Lambda, SQS e DynamoDB
- **AWS Lab:** credenciais temporárias (access key + secret + **session token**)

---

## 1. Credenciais AWS (AWS Lab)

O AWS Lab fornece **credenciais temporárias (STS)**. Configure de uma das formas:

### Opção A — `aws configure` (recomendado)

```bash
aws configure
# AWS Access Key ID:     <do lab>
# AWS Secret Access Key: <do lab>
# Default region name:   us-east-1
# Default output format: json
```

O Lab também fornece um **session token** — se o `aws configure` não o gravar automaticamente, adicione manualmente em `~/.aws/credentials`:

```ini
[default]
aws_access_key_id = <do lab>
aws_secret_access_key = <do lab>
aws_session_token = <do lab>
```

### Opção B — Variáveis de ambiente (`.env`)

Copie o template e preencha com os valores reais:

```bash
cp .env.example .env
# Edite .env com suas credenciais AWS e URLs SQS
```

```bash
export AWS_ACCESS_KEY_ID=<do lab>
export AWS_SECRET_ACCESS_KEY=<do lab>
export AWS_SESSION_TOKEN=<do lab>   # ← OBRIGATÓRIO para DynamoDB real
export AWS_REGION=us-east-1
```

> ⚠️ **Sem o `AWS_SESSION_TOKEN`, chamadas ao DynamoDB real falham** com `The security token included in the request is invalid`.
> A Lambda em produção **não** usa essas credenciais — ela usa a **execution role** (IAM role) anexada à função.

> 🔒 **Segurança**: o `.env` contém segredos e está no `.gitignore`. **Nunca** commite valores reais — use sempre o `.env.example` como template.

---

## 2. Criar as filas SQS na AWS

```bash
# Fila para UserCreatedEvent
aws sqs create-queue \
  --queue-name fcg-user-created \
  --region us-east-1

# Fila para PaymentProcessedEvent
aws sqs create-queue \
  --queue-name fcg-payment-processed \
  --region us-east-1
```

Anote as URLs retornadas — você vai precisar delas no `.env` e nos triggers da Lambda.

---

## 3. Criar a tabela DynamoDB

```bash
aws dynamodb create-table \
  --table-name fcg-notification-users \
  --attribute-definitions AttributeName=Id,AttributeType=S \
  --key-schema AttributeName=Id,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1
```

> Partition key: `Id` (String — Guid serializado). Atributos: `Name`, `Email`, `IsAdmin`, `CreatedAt`.

---

## 4. Ambiente local

A infraestrutura completa (RabbitMQ, APIs e Forwarder) sobe pelo repositório **FCG.Infra**.
Neste repositório só é necessário o DynamoDB Local, para testar sem gastar a conta do Lab:

```bash
docker compose up -d dynamodb-local
```

E aponte `DynamoDB:ServiceURL` para `http://localhost:8000` (já configurado no `appsettings.Development.json`).

### Configuração SMTP (nenhuma credencial fica no código)

`appsettings.json` não contém usuário/senha. Informe por variável de ambiente:

```bash
export Smtp__Username=seu-email@gmail.com
export Smtp__Password=<app-password-do-gmail>
export Smtp__SenderEmail=seu-email@gmail.com
```

No pipeline, esses valores vêm de GitHub Secrets (`SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_SENDER_EMAIL`).

---

## 5. Deploy da Lambda (fluxo completo)

### 5.1 Publicar o código com `dotnet-lambda`

```bash
cd src/FCG.NotificationsAPI

dotnet lambda deploy-function FCG-NotificationsAPI \
  --region us-east-1 \
  --function-runtime dotnet10 \
  --function-handler "FCG.NotificationsAPI::FCG.NotificationsAPI.Functions.NotificationsFunction::FunctionHandler" \
  --function-memory-size 512 \
  --function-timeout 30 \
  --function-role "arn:aws:iam::<ACCOUNT_ID>:role/LabRole" \
  --environment-variables "DynamoDB__TableName=fcg-notification-users;Smtp__Host=smtp.gmail.com;Smtp__Port=587;Smtp__Username=seuemail@gmail.com;Smtp__Password=sua-app-password;Smtp__SenderName=FIAP Cloud Games;Smtp__SenderEmail=seuemail@gmail.com"
```

> **Notas importantes:**
> - **Role**: em Labs AWS Academy, use a role `LabRole` (a role `RoleForLambdaModLabRole` costuma ter `iam:PassRole` negado). O parâmetro correto é `--function-role` (ou `-frole`), **não** `--role`.
> - **`AWS_REGION`**: é variável **reservada** da Lambda — não pode ser definida manualmente. O código já a lê do ambiente automaticamente.
> - O comando publica o projeto, gera o zip (incluindo `appsettings.json` e templates HBS) e cria/atualiza a função.

### 5.2 Configurar os Triggers SQS

```bash
# Trigger para fcg-user-created
aws lambda create-event-source-mapping \
  --function-name FCG-NotificationsAPI \
  --event-source-arn arn:aws:sqs:us-east-1:<ACCOUNT_ID>:fcg-user-created \
  --batch-size 10 \
  --region us-east-1

# Trigger para fcg-payment-processed
aws lambda create-event-source-mapping \
  --function-name FCG-NotificationsAPI \
  --event-source-arn arn:aws:sqs:us-east-1:<ACCOUNT_ID>:fcg-payment-processed \
  --batch-size 10 \
  --region us-east-1
```

### 5.3 Verificar o deploy

```bash
# Estado da função
aws lambda get-function --function-name FCG-NotificationsAPI --region us-east-1 \
  --query "Configuration.{Name:FunctionName,Runtime:Runtime,Handler:Handler,Role:Role,State:State}" --output json

# Estado dos triggers
aws lambda list-event-source-mappings --function-name FCG-NotificationsAPI --region us-east-1 \
  --query "EventSourceMappings[].{UUID:UUID,State:State,EventSourceArn:EventSourceArn}" --output json
```

---

## 6. Teste ponta a ponta

### 6.1 Enviar um `UserCreatedEvent` para a fila

```bash
aws sqs send-message \
  --queue-url "https://sqs.us-east-1.amazonaws.com/<ACCOUNT_ID>/fcg-user-created" \
  --message-body '{"UserId":"a1b2c3d4-0000-0000-0000-000000000001","Name":"Teste","Email":"seuemail@gmail.com","IsAdmin":false,"TemporaryPassword":null}' \
  --message-attributes "EventType={DataType=String,StringValue=UserCreatedEvent}" \
  --region us-east-1
```

### 6.2 Enviar um `PaymentProcessedEvent` para a fila

```bash
aws sqs send-message \
  --queue-url "https://sqs.us-east-1.amazonaws.com/<ACCOUNT_ID>/fcg-payment-processed" \
  --message-body '{"OrderId":"b2c3d4e5-0000-0000-0000-000000000002","UserId":"a1b2c3d4-0000-0000-0000-000000000001","GameId":"c3d4e5f6-0000-0000-0000-000000000003","GameTitle":"Test Game","Price":99.90,"Status":"Approved"}' \
  --message-attributes "EventType={DataType=String,StringValue=PaymentProcessedEvent}" \
  --region us-east-1
```

### 6.3 Verificar o resultado

```bash
# Item no DynamoDB
aws dynamodb get-item \
  --table-name fcg-notification-users \
  --key '{"Id": {"S": "a1b2c3d4-0000-0000-0000-000000000001"}}' \
  --region us-east-1

# Logs da Lambda
aws logs describe-log-streams --log-group-name /aws/lambda/FCG-NotificationsAPI \
  --region us-east-1 --order-by LastEventTime --descending --query "logStreams[0].logStreamName" --output text
```

### 6.4 Teste local (projeto de teste)

O projeto `tests/e2e-test/` executa o código real da Lambda (DI + handlers + DynamoDB + SMTP) localmente:

```bash
set -a; source .env; set +a
dotnet run --project tests/e2e-test/e2e-test.csproj
```

---

## 7. Estrutura do projeto

```
FCG.NotificationsAPI/
├── .github/workflows/lambda-ci-cd.yml  ← build, testes, deploy e infra (SQS, DLQ, DynamoDB, triggers)
├── .env.example
├── docker-compose.yml                  ← apenas dynamodb-local (dev)
├── docs/migracao-dynamodb.md
├── tests/
│   ├── FCG.NotificationsAPI.Tests/     ← testes unitários (rodam no pipeline)
│   └── e2e-test/                       ← ponta a ponta local (usa AWS e SMTP reais; NÃO roda no pipeline)
└── src/FCG.NotificationsAPI/
    ├── Functions/NotificationsFunction.cs   ← entry point Lambda (SQS) com falha parcial de lote
    ├── Handlers/                            ← UserCreatedHandler, PaymentProcessedHandler
    ├── Repositories/                        ← DynamoDB
    ├── Services/, Settings/, Templates/Hbs/
    ├── Program.cs                           ← DI reaproveitado entre invocações (warm start)
    └── appsettings.json
```

### Testes

```bash
dotnet test tests/FCG.NotificationsAPI.Tests
```

---

## 8. Segurança

- **`.env`** contém credenciais reais e está no `.gitignore` — **nunca** commite valores reais
- **`.env.example`** é o template versionado com valores mascarados (placeholders)
- A Lambda usa **execution role** (IAM) — credenciais do Lab são apenas para desenvolvimento local
- O session token do Lab é **temporário** — quando expirar, atualize o `.env` e o `~/.aws/credentials`