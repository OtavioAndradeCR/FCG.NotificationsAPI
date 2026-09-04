#!/bin/sh
# ============================================================
# Configura o Shovel no RabbitMQ para encaminhar mensagens
# das filas locais para filas SQS na AWS.
#
# Variáveis de ambiente necessárias (definidas no compose):
#   RABBITMQ_HOST, RABBITMQ_USER, RABBITMQ_PASS
#   SQS_URL_USER_CREATED     — URL da fila SQS para UserCreatedEvent
#   SQS_URL_PAYMENT_PROCESSED — URL da fila SQS para PaymentProcessedEvent
#   AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, AWS_REGION
# ============================================================

set -e

RABBITMQ_API="http://${RABBITMQ_HOST}:15672/api"
AUTH="${RABBITMQ_USER}:${RABBITMQ_PASS}"

echo "[shovel-setup] Aguardando RabbitMQ HTTP API..."
until curl -sf -u "$AUTH" "${RABBITMQ_API}/overview" > /dev/null; do
  sleep 3
done
echo "[shovel-setup] RabbitMQ pronto."

# --------------------------------------------------------
# Shovel 1: fcg.user-created → SQS user-created
# --------------------------------------------------------
echo "[shovel-setup] Configurando shovel fcg-user-created-to-sqs..."

curl -sf -u "$AUTH" \
  -X PUT "${RABBITMQ_API}/parameters/shovel/%2F/fcg-user-created-to-sqs" \
  -H "Content-Type: application/json" \
  -d "{
    \"value\": {
      \"src-protocol\": \"amqp091\",
      \"src-uri\": \"amqp://${RABBITMQ_USER}:${RABBITMQ_PASS}@${RABBITMQ_HOST}\",
      \"src-queue\": \"fcg.user-created\",
      \"dest-protocol\": \"sqs\",
      \"dest-uri\": \"https://sqs.${AWS_REGION}.amazonaws.com\",
      \"dest-queue\": \"${SQS_URL_USER_CREATED}\",
      \"dest-add-timestamp-header\": true,
      \"add-forward-headers\": true,
      \"publish-properties\": {
        \"MessageAttributes\": {
          \"EventType\": {
            \"DataType\": \"String\",
            \"StringValue\": \"UserCreatedEvent\"
          }
        }
      }
    }
  }"

echo "[shovel-setup] Shovel fcg-user-created-to-sqs configurado."

# --------------------------------------------------------
# Shovel 2: payment.processed → SQS payment-processed
# --------------------------------------------------------
echo "[shovel-setup] Configurando shovel fcg-payment-processed-to-sqs..."

curl -sf -u "$AUTH" \
  -X PUT "${RABBITMQ_API}/parameters/shovel/%2F/fcg-payment-processed-to-sqs" \
  -H "Content-Type: application/json" \
  -d "{
    \"value\": {
      \"src-protocol\": \"amqp091\",
      \"src-uri\": \"amqp://${RABBITMQ_USER}:${RABBITMQ_PASS}@${RABBITMQ_HOST}\",
      \"src-queue\": \"payment.processed\",
      \"dest-protocol\": \"sqs\",
      \"dest-uri\": \"https://sqs.${AWS_REGION}.amazonaws.com\",
      \"dest-queue\": \"${SQS_URL_PAYMENT_PROCESSED}\",
      \"dest-add-timestamp-header\": true,
      \"add-forward-headers\": true,
      \"publish-properties\": {
        \"MessageAttributes\": {
          \"EventType\": {
            \"DataType\": \"String\",
            \"StringValue\": \"PaymentProcessedEvent\"
          }
        }
      }
    }
  }"

echo "[shovel-setup] Shovel fcg-payment-processed-to-sqs configurado."
echo "[shovel-setup] Configuração concluída!"
