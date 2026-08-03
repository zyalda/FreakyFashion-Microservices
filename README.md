# FreakyFashion Microservices

Welcome to the backend architecture for **FreakyFashion**, a modern e-commerce platform built using an event-driven microservices architecture with **.NET 10.0 Isolated Worker Model**, **Azure Functions**, **Docker**, **RabbitMQ**, and **Azure Cosmos DB (NoSQL)**.

## Architecture Overview

This project is configured as a **Mono-repo** containing isolated microservices that communicate completely asynchronously using the **Database-per-Service** and **Publisher-Subscriber** patterns to ensure maximum scalability and decoupling.

*   **OrderService (Publisher):** An HTTP-triggered Azure Function that receives orders from user bash terminal, stores them as raw JSON documents in Cosmos DB, and broadcasts an `OrderCreated` event to a Fanout Exchange.
*   **Message Broker (RabbitMQ):** Handles async broadcast events using a `Fanout Exchange` to distribute events instantly to all connected subscribers.
*   **Inventory & Payment Services (Subscribers):** Independent background worker services running as `BackgroundService` threads that listen to the broker and handle stock reduction and payment transactions simultaneously without blocking.

## Port Topology (Local Environment)

To avoid network collisions during multi-service execution, the local gateway ports are explicitly mapped as follows:
*   `OrderService` (HTTP Gateway): Port `7071`
*   `InventoryService` (Message Consumer): Port `7072`
*   `PaymentService` (Message Consumer): Port `7073`

  ## Tech Stack & Local Infrastructure Containerization

All external infrastructure components are completely containerized using local **Docker Desktop (WSL 2 / Ubuntu Engine)**. Instead of installing heavy local database instances, components are pulled as lightweight official images to maintain a clean workspace:

*   **.NET 10.0 C#** (Modern Isolated Worker Host Architecture).
*   **RabbitMQ v7 Broker (Docker Container):** Pulled as `rabbitmq:3-management`. Configured with an asynchronous eventing consumer loop to handle event broadcast routing.
*   **Azure Cosmos DB vNext (Docker Container):** Pulled as `azure-cosmos-emulator:vnext-latest`. Emulates a production NoSQL Database locally, communicating securely over an HTTPS Gateway on port 8081.
*   **Azurite (Docker Container):** Pulled as `azure-storage/azurite`. Emulates the Azure Storage account required natively by the Azure Functions internal runtime engine.

## Local Environment Setup

To run this infrastructure locally via Docker, execute the following commands:

```bash
# 1. Start RabbitMQ Broker (Management Portal on port 15672)
docker run -d --name freakyfashion-broker -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# 2. Start Azure Cosmos DB vNext Emulator (Data Explorer on port 1234)
docker run --detach --name freakyfashion-cosmosdb --publish 8081:8081 --publish 8080:8080 --publish 1234:1234 ://microsoft.com
```

## Execution Instructions

Launch each microservice inside its respective directory to spin up the local worker threads:

```bash
# Terminal 1 - OrderService
cd src/FreakyFashion-OrderService && func start

# Terminal 2 - InventoryService
cd src/FreakyFashion-InventoryService && func start --port 7072

# Terminal 3 - PaymentService
cd src/FreakyFashion-PaymentService && func start --port 7073

# Triggering an Order via CLI
curl -X POST http://localhost:7071/api/orders -H "Content-Type: application/json" -d '{"CustomerId": "cyber-body-2026", "TotalPrice": 135.00, "Items": ["Freaky Leather Pants", "Matrix Sunglasses"]}'
```
