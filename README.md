# FCG.NotificationsAPI — AWS Lambda

Microsserviço de Notificações da plataforma **FIAP Cloud Games** refatorado para **AWS Lambda**, triggerado por filas **SQS**.

---

## Arquitetura

```
UsersAPI ──────► RabbitMQ (fcg.user-created)    ──► Shovel ──► SQS fcg-user-created     ──► Lambda
PaymentsAPI ───► RabbitMQ (payment.processed)   ──► Shovel ──► SQS fcg-payment-processed ──► Lambda
```

O **RabbitMQ Shovel** encaminha automaticamente as mensagens das filas locais para as filas SQS na AWS. O Lambda é triggerado pelo SQS e processa cada mensagem de forma serverless.

---

## Pré-requisitos

- .NET 10 SDK
- AWS CLI configurado (`aws configure`)
- Ferramenta dotnet-lambda: `dotnet tool install -g Amazon.Lambda.Tools`
- Conta AWS com permissões para Lambda, SQS e RDS/VPC

---

## 1. Criar as filas SQS na AWS

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

Anote as URLs retornadas — você vai precisar delas no `.env` e nas variáveis Lambda.

---

## 2. Configurar o ambiente local (FCG.Infra)

```bash
# Na pasta do FCG.Infra, copie e preencha o .env
cp .env.example .env
# Edite .env com suas credenciais AWS e URLs SQS

# Suba o compose (RabbitMQ com Shovel configurado automaticamente)
docker compose up -d
```

O serviço `rabbitmq-setup` vai configurar automaticamente os Shovels via HTTP API.

---

## 3. Criar a Lambda na AWS

### Via Console AWS
1. Acesse **Lambda → Create function**
2. Runtime: **.NET 10 (arm64 ou x86_64)**
3. Handler: `FCG.NotificationsAPI::FCG.NotificationsAPI.Functions.NotificationsFunction::FunctionHandler`
4. Memória: **512 MB** | Timeout: **30 segundos**

### Variáveis de ambiente da Lambda (Configuration → Environment variables)
| Variável | Valor |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Host=<host-rds>;Port=5432;Database=notifications;Username=fcg_user;Password=fcg_password` |
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__Username` | `seuemail@gmail.com` |
| `Smtp__Password` | `sua-app-password` |
| `Smtp__SenderName` | `FIAP Cloud Games` |
| `Smtp__SenderEmail` | `seuemail@gmail.com` |

---

## 4. Fazer o deploy do código

```bash
cd src/FCG.NotificationsAPI

dotnet lambda deploy-function FCG-NotificationsAPI \
  --region us-east-1 \
  --function-runtime dotnet10 \
  --function-handler "FCG.NotificationsAPI::FCG.NotificationsAPI.Functions.NotificationsFunction::FunctionHandler" \
  --function-memory-size 512 \
  --function-timeout 30
```

---

## 5. Configurar o Trigger SQS na Lambda

```bash
# Trigger para fcg-user-created
aws lambda create-event-source-mapping \
  --function-name FCG-NotificationsAPI \
  --event-source-arn arn:aws:sqs:us-east-1:123456789012:fcg-user-created \
  --batch-size 10 \
  --region us-east-1

# Trigger para fcg-payment-processed
aws lambda create-event-source-mapping \
  --function-name FCG-NotificationsAPI \
  --event-source-arn arn:aws:sqs:us-east-1:123456789012:fcg-payment-processed \
  --batch-size 10 \
  --region us-east-1
```

---

## Estrutura do projeto

```
FCG.NotificationsAPI-Lambda/
├── .env.example                        ← variáveis AWS para o compose
├── docker-compose.yml                  ← FCG.Infra atualizado (sem container notifications)
├── rabbitmq/
│   ├── enabled_plugins                 ← habilita Shovel no RabbitMQ
│   └── shovel-setup.sh                 ← configura Shovels via HTTP API
└── src/FCG.NotificationsAPI/
    ├── Functions/
    │   └── NotificationsFunction.cs    ← entry point Lambda (trigger SQS)
    ├── Handlers/
    │   ├── UserCreatedHandler.cs
    │   └── PaymentProcessedHandler.cs
    ├── Startup.cs                      ← DI reutilizado entre invocações (warm start)
    ├── Extensions/
    ├── Data/, Entities/, Repositories/
    ├── Services/, Settings/
    ├── Templates/Hbs/
    ├── aws-lambda-tools-defaults.json
    └── appsettings.json
```
