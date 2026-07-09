# FCG.NotificationsAPI

Microsserviço de **Notificações** da plataforma **FIAP Cloud Games**.  
Consome eventos do broker (RabbitMQ via MassTransit), persiste um espelho local de usuários no PostgreSQL e realiza o disparo simulado de e-mails transacionais.

---

## Responsabilidades

| Evento consumido | Fila | Ação |
|---|---|---|
| `UserCreatedEvent` (usuário padrão) | `fcg.user-created` | Persiste usuário no banco → envia e-mail de boas-vindas |
| `UserCreatedEvent` (admin) | `fcg.user-created` | Persiste usuário no banco → envia e-mail com senha temporária |
| `PaymentProcessedEvent` (Approved) | `fcg.payment-processed` | Busca usuário pelo UserId no banco → envia e-mail de confirmação |
| `PaymentProcessedEvent` (Rejected) | `fcg.payment-processed` | Apenas loga — nenhum e-mail enviado |

> **Simulação:** O envio de e-mail é simulado via `ILogger` no console, conforme especificado no Tech Challenge.

---

## FCG.Shared (submódulo Git)

Os contratos de eventos (`UserCreatedEvent`, `PaymentProcessedEvent`) residem no repositório compartilhado **[IgorAnthonyy/FCG-Shared](https://github.com/IgorAnthonyy/FCG-Shared)**, adicionado como submódulo Git em `src/FCG.Shared`.

### Clonar o projeto com o submódulo

```bash
git clone --recurse-submodules https://github.com/SEU-ORG/FCG.NotificationsAPI.git
```

Se já clonou sem o submódulo:

```bash
git submodule update --init --recursive
```

### Atualizar o submódulo para a versão mais recente

```bash
git submodule update --remote src/FCG.Shared
```

---

## Banco de dados local

Ao consumir `UserCreatedEvent`, o serviço persiste o usuário (Id, Name, Email, IsAdmin) na tabela `notification_users`.  
Quando o `PaymentProcessedEvent` chega com apenas o `UserId`, o serviço faz um SELECT nessa tabela para obter o e-mail e o nome antes de enviar a notificação.

As migrations são aplicadas **automaticamente** na inicialização do serviço.

---

## Estrutura do projeto

```
FCG.NotificationsAPI/
├── .gitmodules                          ← declaração do submódulo FCG.Shared
├── Dockerfile
├── FCG.NotificationsAPI.sln
├── README.md
├── k8s/
│   ├── configmap.yaml
│   ├── secret.yaml
│   └── deployment.yaml
└── src/
    ├── FCG.Shared/                      ← submódulo Git (contratos de eventos)
    │   └── Events/
    │       ├── UserCreatedEvent.cs
    │       └── PaymentProcessedEvent.cs
    └── FCG.NotificationsAPI/
        ├── Consumers/
        │   ├── UserCreatedEventConsumer.cs
        │   └── PaymentProcessedEventConsumer.cs
        ├── Data/
        │   ├── ApplicationDbContext.cs
        │   ├── Configurations/
        │   │   └── NotificationUserConfiguration.cs
        │   └── Migrations/              ← geradas via dotnet ef
        ├── Entities/
        │   └── NotificationUser.cs
        ├── Extensions/
        │   └── ServiceCollectionExtensions.cs
        ├── Repositories/
        │   ├── INotificationUserRepository.cs
        │   └── NotificationUserRepository.cs
        ├── Services/
        │   ├── IEmailNotificationService.cs
        │   └── EmailNotificationService.cs
        ├── Templates/Hbs/
        │   ├── email-welcome.hbs
        │   ├── email-welcome-admin.hbs
        │   └── email-purchase-confirmed.hbs
        ├── appsettings.json
        ├── appsettings.Development.json
        └── Program.cs
```

---

## Variáveis de ambiente

| Variável | Origem K8s | Descrição | Padrão |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | **Secret** | Connection string PostgreSQL | — |
| `RabbitMQ__Host` | ConfigMap | Host do RabbitMQ | `rabbitmq` |
| `RabbitMQ__Username` | **Secret** | Usuário RabbitMQ | `guest` |
| `RabbitMQ__Password` | **Secret** | Senha RabbitMQ | `guest` |
| `RabbitMQ__Queues__UserCreated` | ConfigMap | Nome da fila | `fcg.user-created` |
| `RabbitMQ__Queues__PaymentProcessed` | ConfigMap | Nome da fila | `fcg.payment-processed` |

---

## Gerando as Migrations

```bash
cd src/FCG.NotificationsAPI
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
```

---

## Executando localmente

```bash
# Subir RabbitMQ e PostgreSQL
docker run -d --name rabbitmq -p 5672:5672 rabbitmq:3-management
docker run -d --name postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16

# Rodar o serviço
cd src/FCG.NotificationsAPI
dotnet run
```

---

## Deploy no Kubernetes

```bash
# 1. Build da imagem
docker build -t fcg-notifications-api:latest .

# 2. Carregar no cluster local (Kind)
kind load docker-image fcg-notifications-api:latest

# 3. Criar namespace se necessário
kubectl create namespace fcg

# 4. Aplicar manifestos
kubectl apply -f k8s/

# 5. Verificar
kubectl get pods -n fcg
kubectl logs -n fcg deployment/notifications-api -f
```
