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

> **Envio de E-mail:** O disparo de e-mails é realizado de fato utilizando a biblioteca **MailKit**, conectando-se a um servidor SMTP configurado (por padrão, configurado para Gmail utilizando App Passwords).

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
| `ConnectionStrings__DefaultConnection` | **Secret** | Connection string PostgreSQL | `Host=fiapcloudgames;Database=fcg_notifications;Username=fcg_user;Password=fcg_password` |
| `RabbitMQ__Host` | ConfigMap | Host do RabbitMQ | `rabbitmq` |
| `RabbitMQ__Username` | **Secret** | Usuário RabbitMQ | `fcg_user` |
| `RabbitMQ__Password` | **Secret** | Senha RabbitMQ | `fcg_password` |
| `RabbitMQ__Queues__UserCreated` | ConfigMap | Nome da fila de criação de usuários | `fcg.user-created` |
| `RabbitMQ__Queues__PaymentProcessed` | ConfigMap | Nome da fila de processamento de pagamentos | `fcg.payment-processed` |
| `Logging__LogLevel__Default` | ConfigMap | Nível de log padrão | `Information` |
| `Logging__LogLevel__MassTransit` | ConfigMap | Nível de log do MassTransit | `Information` |
| `Logging__LogLevel__Microsoft.Hosting.Lifetime` | ConfigMap | Nível de log do ciclo de vida da aplicação | `Information` |
| `Smtp__Host` | ConfigMap | Host do servidor SMTP | `smtp.gmail.com` |
| `Smtp__Port` | ConfigMap | Porta do servidor SMTP | `587` |
| `Smtp__Username` | **Secret** | E-mail de autenticação do SMTP | `seuemail@gmail.com` |
| `Smtp__Password` | **Secret** | Senha (App Password) do SMTP | `sua-app-password-aqui` |
| `Smtp__SenderName` | ConfigMap | Nome exibido no remetente dos e-mails | `FIAP Cloud Games` |
| `Smtp__SenderEmail` | **Secret** | Endereço de e-mail do remetente | `seuemail@gmail.com` |

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

> [!IMPORTANT]
> Para que o envio de e-mails funcione de fato (seja rodando localmente, via Docker Compose ou no Kubernetes), é necessário configurar as informações corretas de SMTP (`Smtp__Username` e `Smtp__Password`) nos arquivos de configuração ou variáveis de ambiente correspondentes. Caso contrário, o serviço gerará erros ao tentar realizar os disparos de e-mail.

---

## Deploy no Kubernetes

```bash
# 1. Build da imagem (ou docker pull igoranthony12/notification-api:latest)
docker build -t igoranthony12/notification-api:latest .

# 2. Carregar no cluster local (Kind)
kind load docker-image igoranthony12/notification-api:latest

# 3. Criar namespace se necessário
kubectl create namespace fcg

# 4. Aplicar manifestos
kubectl apply -f k8s/

# 5. Verificar
kubectl get pods -n fcg
kubectl logs -n fcg deployment/notifications-api -f
```
