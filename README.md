# outbox-webhook-dispatcher

A multi-tenant webhook delivery service built with **ASP.NET Core 8** and **SQLite**, implementing the [transactional outbox pattern](https://microservices.io/patterns/data/transactional-outbox.html) for reliable, at-least-once webhook delivery with automatic retries, exponential backoff, and HMAC request signing.

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Quick Start](#quick-start)
- [Webhook Request Format](#webhook-request-format)
- [API Reference](#api-reference)
- [Delivery Lifecycle](#delivery-lifecycle)
- [Configuration](#configuration)
- [Multi-Tenancy](#multi-tenancy)
- [Important Behaviour Notes](#important-behaviour-notes)
- [Development — GitHub Codespaces](#development--github-codespaces)
- [Health Check](#health-check)
- [License](#license)

---

## Overview

When building event-driven systems, reliably delivering webhooks to external subscribers is non-trivial. Network failures, subscriber downtime, and race conditions can all cause missed events.

This project solves that by implementing the **transactional outbox pattern**: events are first persisted to a database, and a background dispatcher handles delivery independently. If delivery fails, the system retries automatically with exponential backoff. Every delivery attempt is tracked for full observability.

The system is **multi-tenant** — a single instance can serve multiple isolated tenants, each with their own subscriptions and message history.

---

## Features

- **Reliable delivery** — messages are persisted before dispatch; delivery is decoupled from the enqueue request
- **Multi-tenancy** — all data is scoped per tenant via the `X-Tenant-Id` header
- **Fan-out** — each enqueued message creates one delivery per active subscription in the tenant
- **Automatic retries** — failed deliveries retry with exponential backoff up to a configurable maximum
- **Per-subscription concurrency** — `MaxConcurrency` limits in-flight deliveries per subscription
- **HMAC signing** — every webhook request is signed with `HMAC-SHA256`, allowing receivers to verify authenticity
- **Full audit trail** — every delivery attempt is recorded with status code, error, and timestamp
- **Manual requeue** — dead or failed deliveries can be manually requeued via the API
- **Secret rotation** — subscription secrets can be rotated without downtime
- **Extra headers** — custom headers can be forwarded with each webhook delivery via `extraHeadersJson`

---

## Tech Stack

| | |
|---|---|
| **Runtime** | .NET 8 |
| **Framework** | ASP.NET Core 8 |
| **ORM** | Entity Framework Core 8 |
| **Database** | SQLite |
| **HTTP Resilience** | Polly |
| **Logging** | Serilog |
| **API Docs** | Swagger / Swashbuckle |

---

## Project Structure
```
src/
└── Outbox.Api/
    ├── Controllers/          # REST API endpoints (Messages, Subscriptions, Deliveries, Attempts)
    ├── Data/                 # EF Core DbContext and design-time factory
    ├── DTOs/
    │   ├── Requests/         # Incoming request models
    │   └── Responses/        # Outgoing response models
    ├── Entities/
    │   └── Enums/            # DeliveryStatus enum
    ├── Interfaces/
    │   ├── IRepositories/    # Repository abstractions
    │   └── IServices/        # Service abstractions
    ├── Migrations/           # EF Core database migrations
    ├── Options/              # Strongly-typed configuration (OutboxDispatcherOptions)
    ├── Repositories/         # EF Core repository implementations
    ├── Services/             # Background dispatcher and webhook sender (HMAC signing)
    ├── Swagger/              # Swagger operation filters (tenant header)
    ├── Tenancy/              # Tenant context, query extensions, HTTP and worker contexts
    └── Utils/                # Shared utilities (e.g. string truncation)
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- EF Core CLI tools:
```bash
dotnet tool install --global dotnet-ef
```

---

## Getting Started

### 1. Clone the repository
```bash
git clone https://github.com/Eaziey/outbox-webhook-dispatcher.git
cd outbox-webhook-dispatcher
```

### 2. Restore dependencies
```bash
cd src/Outbox.Api
dotnet restore
```

### 3. Create the database
```bash
dotnet ef database update
```

This applies all migrations and creates `outbox.db` in the project directory.

### 4. Run the API
```bash
dotnet run
```

The API starts on `http://localhost:5157`. Open Swagger UI at:
```
http://localhost:5157/swagger
```

---

## Quick Start

Every request requires the `X-Tenant-Id` header. You can use any string as a tenant identifier (e.g. `tenant-1`).

### Step 1 — Create a subscription
```http
POST /api/subscriptions
X-Tenant-Id: tenant-1
Content-Type: application/json

{
  "endpoint": "https://your-endpoint.com/webhook",
  "maxConcurrency": 5
}
```

> If `secret` is omitted, one is auto-generated. Store it — you will need it to verify incoming webhook signatures on the receiving end.

### Step 2 — Enqueue a message
```http
POST /api/messages
X-Tenant-Id: tenant-1
Content-Type: application/json

{
  "eventType": "order.created",
  "payload": { "orderId": 123, "amount": 99.99 },
  "subjectKey": "order-123"
}
```

The background dispatcher picks this up within seconds and POSTs it to your endpoint.

### Step 3 — Check delivery status
```http
GET /api/messages/{id}/deliveries
X-Tenant-Id: tenant-1
```

A `status` of `2` means `Sent`. See [Delivery Lifecycle](#delivery-lifecycle) for all status values.

---

## Webhook Request Format

Every webhook delivery is an HTTP POST to the subscription endpoint with the following headers:

| Header | Description |
|---|---|
| `X-Event-Type` | The event type (e.g. `order.created`) |
| `X-Tenant-Id` | The tenant the message belongs to |
| `X-Subject-Key` | Optional subject key for grouping related messages |
| `X-Timestamp` | Unix timestamp of when the request was sent |
| `X-Signature` | HMAC-SHA256 signature for verifying authenticity |
| `X-Signature-Version` | Signature version (currently `v1`) |
| `Idempotency-Key` | Auto-generated unique key per message for deduplication on the receiver side |
| `Content-Type` | `application/json` |

The request body is the raw JSON payload provided when enqueuing the message.

### Verifying the Signature

The signature is computed as:
```
HMAC-SHA256(secret, "{timestamp}.{body}")
```

Example verification in C#:
```csharp
var data = $"{timestamp}.{body}";
using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
var isValid = expected == receivedSignature;
```

---

## API Reference

All endpoints require the `X-Tenant-Id` header. Requests without it receive a `400 Bad Request`.

### Subscriptions

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/subscriptions` | Create a new subscription |
| `GET` | `/api/subscriptions` | List subscriptions (filter by `?isActive=true/false`) |
| `GET` | `/api/subscriptions/{id}` | Get a subscription by ID |
| `PUT` | `/api/subscriptions/{id}` | Update endpoint, secret, isActive, or maxConcurrency |
| `DELETE` | `/api/subscriptions/{id}` | Soft-deactivate a subscription |
| `POST` | `/api/subscriptions/{id}/rotate-secret` | Rotate the signing secret |

### Messages

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/messages` | Enqueue a new message |
| `GET` | `/api/messages` | List messages (filter by `?subjectKey=`) |
| `GET` | `/api/messages/{id}` | Get a message with delivery summary |
| `GET` | `/api/messages/{id}/deliveries` | List deliveries for a message |

### Deliveries

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/deliveries/{id}` | Get a delivery by ID |
| `POST` | `/api/deliveries/{id}/requeue` | Manually requeue a delivery |

### Delivery Attempts

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/messages/{messageId}/attempts` | List all attempts for a message |
| `GET` | `/api/deliveries/{deliveryId}/attempts` | List all attempts for a delivery |

---

## Delivery Lifecycle
```
Pending → Sending → Sent
                 ↘ Failed → (retry with backoff) → Pending
                          → Dead (max attempts reached)
```

| Status | Value | Description |
|---|---|---|
| `Pending` | `0` | Waiting to be dispatched |
| `Sending` | `1` | Currently leased and being dispatched |
| `Sent` | `2` | Successfully delivered (2xx response) |
| `Failed` | `3` | Last attempt failed, scheduled for retry |
| `Dead` | `4` | Max attempts reached, no further retries |

Dead deliveries can be manually requeued via `POST /api/deliveries/{id}/requeue`.

---

## Configuration

All dispatcher settings live in `appsettings.json` under `OutboxDispatcher`:
```json
{
  "OutboxDispatcher": {
    "LeaseBatchSize": 50,
    "LeaseDurationSeconds": 300,
    "LoopDelayMilliseconds": 2000,
    "DefaultMaxAttempts": 5,
    "MaxBackoffSeconds": 600,
    "BackoffBaseSeconds": 5
  }
}
```

| Setting | Default | Description |
|---|---|---|
| `LeaseBatchSize` | `50` | Max deliveries processed per loop iteration |
| `LeaseDurationSeconds` | `300` | How long a delivery is leased before it becomes eligible for retry |
| `LoopDelayMilliseconds` | `2000` | Polling interval when no deliveries are pending |
| `DefaultMaxAttempts` | `5` | Max attempts before a delivery is marked Dead |
| `MaxBackoffSeconds` | `600` | Maximum delay cap between retries |
| `BackoffBaseSeconds` | `5` | Base multiplier for exponential backoff |

Backoff delay formula: `min(MaxBackoffSeconds, 2^(attempt-1) * BackoffBaseSeconds)`

So with defaults: 5s → 10s → 20s → 40s → 80s → capped at 600s.

---

## Multi-Tenancy

Tenant identity is caller-supplied via the `X-Tenant-Id` request header. All data — messages, subscriptions, deliveries, and attempts — is fully isolated per tenant. The background dispatcher processes deliveries across all tenants. Tenant registration and management is intentionally out of scope; this service trusts whatever tenant ID is passed in.

---

## Important Behaviour Notes

- **Fan-out happens at enqueue time** — only subscriptions that are active when a message is enqueued will receive a delivery. Reactivating a subscription does not retroactively deliver previously enqueued messages.
- **Soft deletes** — deleting a subscription sets `isActive = false`. It is never physically removed from the database.
- **Idempotency keys** — each message is automatically assigned a unique idempotency key which is forwarded to the receiver as the `Idempotency-Key` header. This allows receivers to detect and deduplicate retried deliveries.
- **Extra headers** — pass a valid JSON object as `extraHeadersJson` when enqueuing a message to forward custom headers with every webhook delivery for that message. Example: `"extraHeadersJson": "{\"X-Custom-Header\": \"value\"}"`.
- **HTTPS only** — subscription endpoints must use HTTPS. HTTP endpoints are rejected with a `400` validation error.
- **Lease mechanism** — the dispatcher uses `NextAttemptUtc` as a lease timestamp to prevent double-delivery in concurrent environments. If a delivery is not completed within `LeaseDurationSeconds`, it becomes eligible for retry.
- **Secret rotation** — use `POST /api/subscriptions/{id}/rotate-secret` to generate a new signing secret. Update your receiver to use the new secret immediately after rotating.

---

## Development — GitHub Codespaces

This repo includes a `.devcontainer` configuration. Opening it in GitHub Codespaces will automatically provision a .NET 8 environment with the EF Core CLI pre-installed. Then simply run:
```bash
cd src/Outbox.Api
dotnet ef database update
dotnet run
```

---

## Health Check
```
GET /health
```

Returns `200 OK` when the service is running.

---

## License

This project is licensed under the [MIT License](LICENSE).
