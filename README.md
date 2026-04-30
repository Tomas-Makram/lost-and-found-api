<div align="center">

# 🔎 Lost & Found API

**A production-ready RESTful API for managing lost and found items — built with ASP.NET Core 8**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-9.0-512BD4?style=flat-square&logo=dotnet)](https://docs.microsoft.com/ef/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?style=flat-square&logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![Redis](https://img.shields.io/badge/Redis-Cache-DC382D?style=flat-square&logo=redis)](https://redis.io/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--time-512BD4?style=flat-square&logo=dotnet)](https://docs.microsoft.com/aspnet/signalr)
[![JWT](https://img.shields.io/badge/Auth-JWT-000000?style=flat-square&logo=jsonwebtokens)](https://jwt.io/)
[![Swagger](https://img.shields.io/badge/Docs-Swagger-85EA2D?style=flat-square&logo=swagger)](https://swagger.io/)

</div>

---

## 📋 Table of Contents

- [Overview](#-overview)
- [Features](#-features)
- [Architecture](#-architecture)
- [Tech Stack](#-tech-stack)
- [Getting Started](#-getting-started)
- [Configuration](#-configuration)
- [API Reference](#-api-reference)
- [Real-Time (SignalR)](#-real-time-signalr)
- [Security](#-security)
- [Database Schema](#-database-schema)
- [Project Structure](#-project-structure)

---

## 🌐 Overview

**Lost & Found** is a platform that connects people who have lost belongings with those who have found them. A finder uploads an item with a verification question — only the real owner would know the answer. Once submitted, an admin reviews and approves the listing before it goes public.

The platform handles the full lifecycle:

```
Finder reports item → Admin reviews → Published → Owner answers question → Admin approves claim → Item returned
```

---

## ✨ Features

### Core Workflow
- **Found Item Reporting** — Finders submit items with photos, location, reward percentage, and a secret verification question
- **Admin Review Pipeline** — Every item is reviewed before going public (Approve / Reject / Block with notes)
- **One-Attempt Claim System** — Each user gets exactly one chance to answer the verification question per item
- **Claim Approval** — Admins manually approve or reject claims after reviewing the answer

### Authentication & Security
- **JWT Access Tokens** with configurable expiry and refresh token rotation
- **Multi-session support** — users can be logged in from multiple devices simultaneously
- **OTP Email Verification** — accounts must be verified before posting items
- **Forgot Password flow** — OTP → session token → reset password
- **BCrypt password hashing** with configurable work factor
- **AES Data Encryption** via ASP.NET Core Data Protection for PII fields (email, phone, national ID)
- **SHA-256 hashing** for fast equality lookups on encrypted fields
- **Account lockout** after consecutive failed login attempts

### Real-Time Features
- **SignalR Chat** — live messaging between finders and claimants
- **SignalR Notifications** — instant push notifications for item status changes, claim updates, and messages

### Platform Management
- **Admin Dashboard** — platform-wide statistics
- **User Management** — block / unblock users, view activity
- **Image Upload** — up to 5 images per item (local storage, swappable to cloud)
- **Geo Search** — find items near a location using the Haversine formula

### Infrastructure
- **Multi-layer Rate Limiting** — global IP limiter + per-endpoint sliding/fixed window policies
- **Distributed User Throttle** — middleware with Redis or in-memory backend
- **Redis / Memory Cache** — configurable at runtime via `appsettings.json`
- **Response Compression** — HTTPS-enabled gzip
- **Security Headers** — CSP, HSTS, X-Frame-Options, Permissions-Policy, CORP, COEP
- **API Versioning** — URL segment versioning (`/api/v1/`)
- **Automatic DB Migration + Seeding** — database and default admin created on first run
- **Structured Logging** — request/response tracing with EF Core command logging

---

## 🏗 Architecture

The solution follows a **3-layer architecture**:

```
┌─────────────────────────────────────────────┐
│               Presentation Layer             │
│         Controllers  ·  SignalR Hubs         │
├─────────────────────────────────────────────┤
│               Business Layer                 │
│  Services  ·  DTOs  ·  Filters  ·  Middleware│
├─────────────────────────────────────────────┤
│                 Data Layer                   │
│          EF Core Models  ·  DBContext        │
└─────────────────────────────────────────────┘
              ↕ SQL Server  ↕ Redis
```

---

## 🛠 Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core 8 |
| ORM | Entity Framework Core 9 |
| Database | Microsoft SQL Server |
| Cache | Redis (StackExchange.Redis) / In-Memory |
| Real-Time | ASP.NET Core SignalR |
| Authentication | JWT Bearer + Refresh Tokens |
| Password Hashing | BCrypt.Net-Next |
| Data Encryption | ASP.NET Core Data Protection |
| Email | SMTP (Gmail) |
| API Docs | Swagger / OpenAPI |
| API Versioning | Asp.Versioning |
| Rate Limiting | ASP.NET Core RateLimiter + Custom Middleware |

---

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/sql-server) (or SQL Server Express)
- [Redis](https://redis.io/) (optional — falls back to in-memory cache)
- [Git](https://git-scm.com/)

### Installation

**1. Clone the repository**

```bash
git clone https://github.com/YOUR_USERNAME/lost-and-found-api.git
cd lost-and-found-api
```

**2. Restore dependencies**

```bash
dotnet restore
```

**3. Configure the application**

Copy the example settings and fill in your values:

```bash
cp appsettings.json appsettings.Development.json
```

Edit `appsettings.Development.json` — at minimum update:
- `ConnectionStrings.Connection`
- `EmailSettings` (SMTP credentials)
- `Jwt.Key` (must be at least 32 characters)
- `AdminSeed` (default admin credentials)

**4. Apply database migrations**

```bash
dotnet ef database update
```

> The `AdminSeeder` runs automatically on first startup and creates the default admin account.

**5. Run the application**

```bash
dotnet run
```

Open [https://localhost:7214/swagger](https://localhost:7214/swagger) to explore the API.

---

## ⚙️ Configuration

Key sections in `appsettings.json`:

```jsonc
{
  "ConnectionStrings": {
    "Connection": "Server=...;Database=LostAndFound;..."
  },

  "Jwt": {
    "Key": "YOUR_SECRET_KEY_MIN_32_CHARS",
    "Issuer": "LostAndFoundAPI",
    "Audience": "LostAndFoundUsers",
    "DurationInMinutes": 60
  },

  "AdminSeed": {
    "Email":    "admin@lostandfound.com",
    "Password": "Admin@1234",
    "UserName": "admin",
    "FullName": "System Administrator",
    "Phone":    "01000000000",
    "NationalId": "00000000000000",
    "Address":  "Cairo, Egypt",
    "Latitude": "30.044420",
    "Longitude": "31.235712"
  },

  "CacheSettings": {
    "Provider": "Redis",
    "RedisConnection": "localhost:6379"
  },

  "OtpSettings": {
    "OtpExpiryMinutes": 10,
    "MaxOtpAttempts": 3,
    "ResendCooldownMinutes": 2
  }
}
```

> **Security note:** Never commit real credentials. Use environment variables or `appsettings.Development.json` (excluded from Git) for sensitive values.

---

## 📡 API Reference

Base URL: `https://localhost:7214/api/v1`

### Authentication

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/authenticate/Signup` | Public | Register a new account |
| `POST` | `/authenticate/Login` | Public | Login and receive tokens |
| `GET` | `/authenticate/me` | ✅ | Get my profile |
| `POST` | `/authenticate/Logout` | ✅ | Logout current session |
| `POST` | `/authenticate/refresh-token` | Public | Rotate refresh token |
| `POST` | `/authenticate/changePassword` | ✅ | Change password |
| `POST` | `/authenticate/changeFields` | ✅ | Update profile fields |
| `POST` | `/authenticate/sendVerificationOtp` | ✅ | Send account verification OTP |
| `POST` | `/authenticate/verifyAccountOtp` | ✅ | Verify account with OTP |
| `POST` | `/authenticate/forgotPassword/requestOtp` | Public | Request password reset OTP |
| `POST` | `/authenticate/forgotPassword/verifyOtp` | Public | Verify reset OTP |
| `POST` | `/authenticate/forgotPassword/reset` | Public | Reset password with token |

### Found Items

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/founditems` | Public | Browse published items |
| `GET` | `/founditems/{id}` | Public | Get item details |
| `POST` | `/founditems` | ✅ Verified | Report a found item |
| `PUT` | `/founditems/{id}` | ✅ Owner | Update item |
| `DELETE` | `/founditems/{id}` | ✅ Owner | Archive item |
| `GET` | `/founditems/mine` | ✅ | My reported items |
| `POST` | `/founditems/claim` | ✅ Verified | Submit a claim attempt |
| `GET` | `/founditems/{id}/claims` | ✅ Owner/Admin | View claim attempts |

**Query parameters for `GET /founditems`:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `category` | string | Filter by category |
| `keyword` | string | Search in title and description |
| `nearLatitude` | decimal | Geo search center latitude |
| `nearLongitude` | decimal | Geo search center longitude |
| `radiusKm` | double | Search radius in kilometres |
| `page` | int | Page number (default: 1) |
| `pageSize` | int | Items per page (default: 20) |

### Images

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `POST` | `/images/upload` | ✅ | Upload up to 5 images (multipart/form-data) |
| `DELETE` | `/images?url=...` | ✅ | Delete an image by URL |

### Chat

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/chat/conversations` | ✅ | My conversations inbox |
| `GET` | `/chat/conversation/{itemId}/{userId}` | ✅ | Conversation history |
| `POST` | `/chat/send` | ✅ | Send a message (REST fallback) |
| `POST` | `/chat/read/{itemId}/{userId}` | ✅ | Mark conversation as read |
| `GET` | `/chat/unread` | ✅ | Unread messages count |

### Notifications

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| `GET` | `/notifications` | ✅ | My notifications |
| `GET` | `/notifications/unread-count` | ✅ | Unread count |
| `POST` | `/notifications/mark-all-read` | ✅ | Mark all as read |
| `POST` | `/notifications/{id}/mark-read` | ✅ | Mark one as read |

### Admin

> All admin endpoints require `Role: Admin`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/admin/dashboard` | Platform statistics |
| `GET` | `/admin/items` | All items (filterable by status) |
| `POST` | `/admin/items/review` | Approve / Reject / Block an item |
| `DELETE` | `/admin/items/{id}` | Remove an item |
| `GET` | `/admin/items/{id}/claims` | View item claims |
| `POST` | `/admin/claims/review` | Approve / Reject a claim |
| `GET` | `/admin/users` | User list |
| `POST` | `/admin/users/manage` | Block / Unblock a user |

---

## ⚡ Real-Time (SignalR)

### Connect

```javascript
// Notifications
const notifications = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", {
    accessTokenFactory: () => localStorage.getItem("token")
  })
  .withAutomaticReconnect()
  .build();

await notifications.start();

notifications.on("ReceiveNotification", (notification) => {
  console.log(notification);
  // { id, title, body, type, relatedItemId, isRead, createdAt }
});
```

```javascript
// Chat
const chat = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/chat", {
    accessTokenFactory: () => localStorage.getItem("token")
  })
  .withAutomaticReconnect()
  .build();

await chat.start();

// Join a chat room for a specific item
await chat.invoke("JoinItemChat", "ITEM_GUID");

// Send a message
await chat.invoke("SendMessage", "ITEM_GUID", "RECIPIENT_GUID", "Hello!");

// Receive messages
chat.on("ReceiveMessage", (message) => {
  console.log(message);
  // { id, foundItemId, senderId, senderName, content, sentAt, isMine, ... }
});
```

---

## 🔒 Security

| Mechanism | Implementation |
|-----------|----------------|
| Authentication | JWT Bearer with `ClockSkew: Zero` |
| Token rotation | Refresh token hashed in DB, rotated on every use |
| Session validation | `RequireActiveLogin` filter checks DB on every request |
| Password hashing | BCrypt with work factor 13 |
| PII encryption | AES via ASP.NET Core Data Protection |
| PII lookup | SHA-256 hash stored alongside cipher for O(1) search |
| Rate limiting | Global IP limiter + 8 per-endpoint policies |
| User throttling | Distributed middleware (Redis-backed in production) |
| Security headers | CSP, HSTS, X-Frame-Options, Permissions-Policy |
| CORS | Allowlist-based with `AllowCredentials` for SignalR |
| Account lockout | Auto-block after 5 failed login attempts |
| OTP brute-force | Per-flow attempt limits + temporary block windows |
| Mail flood protection | Global mail action window (20 mails / 30 days) |

---

## 🗄 Database Schema

```
Users
 ├── UserSessions          (multi-device login)
 ├── PaymentTransactions
 ├── Wallet → WalletTransactions
 ├── FoundItems (reported)
 │    ├── ClaimAttempts    (unique per user per item)
 │    └── ChatMessages
 └── Notifications
```

---

## 📁 Project Structure

```
solution/
├── BackendAPILorenSameh/          ← ASP.NET Core host
│   ├── Controllers/
│   │   ├── AuthenticateController.cs
│   │   ├── FoundItemsController.cs
│   │   ├── AdminController.cs
│   │   ├── ChatController.cs
│   │   ├── NotificationsController.cs
│   │   └── ImagesController.cs
│   ├── AdminSeeder.cs
│   ├── Program.cs
│   └── appsettings.json
│
├── BusinessLayer/                 ← Services, DTOs, Hubs
│   ├── DTOs/
│   ├── Services/
│   │   ├── FoundItemService.cs
│   │   ├── ChatService.cs
│   │   ├── NotificationService.cs
│   │   ├── OtpService.cs
│   │   ├── TokenSessionService.cs
│   │   ├── MailService.cs
│   │   └── ImageStorageService.cs
│   ├── Hubs/
│   │   ├── ChatHub.cs
│   │   └── NotificationHub.cs
│   ├── Filters/
│   ├── Middleware/
│   └── Functions/
│       └── Authenticate.cs
│
└── DataLayer/                     ← EF Core models
    └── Models/
        ├── User.cs
        ├── UserSession.cs
        ├── FoundItem.cs
        ├── ClaimAttempt.cs
        ├── ChatMessage.cs
        ├── Notification.cs
        ├── Wallet.cs
        ├── PaymentTransaction.cs
        └── DBContext.cs
```

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

<div align="center">
Built with ❤️ using ASP.NET Core 8
</div>
