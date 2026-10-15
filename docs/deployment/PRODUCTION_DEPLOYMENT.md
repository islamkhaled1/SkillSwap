# SkillSwap — Production Deployment & Operational Guide

**Hosting Provider:** MonsterASP.NET  
**Production API Base URL:** `https://skill-swap.runasp.net`  
**Swagger UI:** `https://skill-swap.runasp.net/swagger/index.html`  
**Health Check Endpoint:** `https://skill-swap.runasp.net/health`  
**Production Deployment & Hosting:** [Developer 1 — Islam Khaled](https://github.com/islamkhaled1) (@islamkhaled1)  
**Status:** Live in Production

---

## 1. Overview & Architecture

SkillSwap's backend is deployed on MonsterASP.NET utilizing Windows Server with IIS 10.0 and the ASP.NET Core Module v2 (ANCM) in InProcess hosting mode, backed by Microsoft SQL Server 2025.

The production deployment consists of:
- **Backend API Runtime:** .NET 10 (`net10.0`), ASP.NET Core Web API, Entity Framework Core 10.
- **Relational Persistence:** Microsoft SQL Server 2025 Express, running relational tables, unique constraints, and append-only financial ledgers with row-level concurrency locks (`UPDLOCK, ROWLOCK`).
- **Real-Time Communication:** ASP.NET Core SignalR `ChatHub` accessible at `wss://skill-swap.runasp.net/hubs/chat`.
- **Identity & Security:** ASP.NET Core Identity with HMAC-SHA256 JWT bearer token authentication and startup key validation.

---

## 2. Production Environment Configuration

All production configuration is injected via operating system environment variables managed in the MonsterASP.NET Control Panel (**Websites → [site] → Scripting → Environment variables**). No secrets or credentials are hardcoded in the codebase, `appsettings.json`, or Git.

### Required Environment Variables

| Variable Name | Description | Example / Required Format |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Declares ASP.NET Core runtime environment. | `Production` |
| `ConnectionStrings__DefaultConnection` | Database connection string using the provider-supplied **Local Connection String**. | Obtained from Control Panel (see Section 3). |
| `Jwt__SecretKey` | Cryptographically secure symmetric signing key for HMAC-SHA256 (minimum 32 bytes / 256 bits). | 64+ character random Base64 or Base64URL string. |
| `Jwt__Issuer` | Trusted JWT token issuer claim. | `SkillSwap` |
| `Jwt__Audience` | Intended token audience claim. | `SkillSwap.Client` |
| `Swagger__Enabled` | Enables Swagger UI at `/swagger/index.html` in Production for API evaluation. | `true` |
| `AdminUser__Email` | Email address for initial administrator account provisioning. | Dedicated admin email (e.g., `admin@skillswap.local`). |
| `AdminUser__Password` | High-entropy password for initial administrator. | Complex password with letters, numbers, and symbols. |
| `AdminUser__DisplayName` | Profile display name for the initial administrator. | `Platform Administrator` |

> [!CAUTION]
> **Administrator Provisioning Security Warning:**  
> The `IdentityRoleSeeder` checks whether an account with `AdminUser__Email` already exists. If found, it automatically **elevates the account to the `Admin` role**. Never configure the email address of an ordinary user as `AdminUser__Email`.

---

## 3. Database Connection Configuration

MonsterASP.NET provides two connection string variants in the Control Panel under **Databases → [database] → Users → Connection string**:

```
MonsterASP.NET Database Panel
├── Local Connection String   ──► Used by the API hosted on MonsterASP.NET
└── Remote Connection String  ──► Used by external tools (SSMS from developer PC)
```

### Local Connection String (Mandatory for Hosted API)
- The hosted web application must use the **Local Connection String** copied from `Databases → [database] → Users → Connection string → Local access`.
- **Why Local Access is Required:** The local string connects through MonsterASP.NET's internal datacenter network. It offers minimum latency, avoids public Internet routing, and prevents connection drops caused by external firewall rules.
- Set `ConnectionStrings__DefaultConnection` to this exact string in the Control Panel.

### Remote Connection String (External Tooling Only)
- The **Remote Connection String** is reserved for external database management (e.g., connecting via SQL Server Management Studio from a developer workstation).
- External connections require enabling remote access and whitelisting your client IP address in the Control Panel under **Databases → [database] → Allow Remote Access**.

---

## 4. Database Migrations & Verification

SkillSwap uses Entity Framework Core migrations to manage schema evolution. The baseline migration history consists of three sequential migrations:
1. `20261008143422_InitialCreate`: Core schema, 22 tables, relations, and initial subscription plan seed.
2. `20261008154444_HardenPhase3Contracts`: Nullable plan pricing contract hardening.
3. `20261010190051_AddCreditTransactionIdempotencyKey`: Append-only credit ledger `IdempotencyKey` column and filtered unique index.

### Post-Migration Verification Queries

After applying migrations, execute these queries in the MonsterASP.NET Control Panel (**Run T-SQL**) or via SSMS:

```sql
-- 1. Verify all three migrations are recorded
SELECT [MigrationId], [ProductVersion]
FROM [__EFMigrationsHistory]
ORDER BY [MigrationId];

-- 2. Verify the default FREE subscription plan is seeded and active
SELECT [Id], [Code], [Name], [IsActive], [MonthlyLearningMinutes], [Price]
FROM [SubscriptionPlans]
WHERE [Code] = 'FREE';

-- 3. Verify the filtered unique idempotency index is active
SELECT [name], [is_unique], [filter_definition]
FROM sys.indexes
WHERE [object_id] = OBJECT_ID('CreditTransactions')
  AND [name] = 'UX_CreditTransaction_IdempotencyKey';
```

Expected results:
- Exactly 3 rows in `__EFMigrationsHistory`.
- Exactly 1 row for plan `FREE` with `MonthlyLearningMinutes = 180`, `Price = 0.00`, and `IsActive = 1`.
- Exactly 1 row for `UX_CreditTransaction_IdempotencyKey` with `is_unique = 1` and `filter_definition = ([IdempotencyKey] IS NOT NULL)`.

### Safe Migration Recovery Procedures

> [!WARNING]
> **Do Not Blindly Retry Failed Migration Scripts:**  
> The idempotent SQL migration script wraps migration blocks in `IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = ...)`. If a statement fails midway through a migration block, partial schema objects (such as tables or columns) may exist while the migration completion record is missing.  
> 
> Retrying the entire script blindly will attempt to re-execute `CREATE TABLE` statements for already-created objects, resulting in collision errors (e.g., `Msg 2714: There is already an object named '...' in the database`).

**Recommended Recovery Workflow:**
1. **Review Import Log:** Inspect the MonsterASP.NET Import Detail log or SQL output to identify the exact failing statement and error code.
2. **Inspect Migration History:** Query `SELECT * FROM [__EFMigrationsHistory]` to identify which migrations actually committed.
3. **Inspect Schema Objects:** Query `sys.tables`, `sys.columns`, and `sys.indexes` to determine whether orphaned partial changes remain.
4. **Clean Baseline Restart (Fresh Database):** If the database is new and contains no valuable data, the safest resolution is dropping and recreating the empty database in the MonsterASP.NET Control Panel, then re-importing cleanly.

---

## 5. Real-Time Communication (SignalR)

- **WebSocket Endpoint:** `https://skill-swap.runasp.net/hubs/chat`
- **Protocol:** SignalR WebSocket transport (`wss://`) with fallback to Server-Sent Events or Long Polling.
- **Authentication:** Clients authenticate by passing their JWT token in the query string (`?access_token=<jwt>`), which is intercepted by `AuthenticationExtensions.cs`.
- **Group Authorization:** `ChatHub.JoinConversation` verifies caller membership via `ConversationParticipants` before adding the connection to the group (`conversation_{id}`). Non-participants receive an authorization rejection.
- **Persistence First:** Messages are written to SQL Server via `ChatService.SendMessageAsync` before being broadcast to group members via `Clients.Group(...).SendAsync("ReceiveMessage", ...)`.

---

## 6. Production Logging & Troubleshooting

### Enabling IIS Stdout Logging
In `web.config`, the ASP.NET Core module can capture startup crash logs:

```xml
<aspNetCore processPath="dotnet" arguments=".\SkillSwap.API.dll" stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" hostingModel="inprocess" />
```

- When `stdoutLogEnabled="true"` and a `logs/` directory exists under `wwwroot`, any startup crash or unhandled exception is written to `wwwroot/logs/stdout_<timestamp>_<pid>.log`.
- To inspect logs, connect via SFTP using your MonsterASP.NET SFTP host and credentials (e.g., `<site-username>.siteasp.net:22`) and navigate to `/wwwroot/logs`.
- Once verified stable, set `stdoutLogEnabled="false"` to prevent unnecessary disk I/O.

### Application Pool Recycling
Whenever environment variables or configuration files are updated:
1. Navigate to **Websites → [site] → Process**.
2. Click **Restart application (AppPool)** to ensure worker processes reload configuration cleanly.

---

## 7. Hosting Plan Considerations

On MonsterASP.NET evaluation/free hosting tiers:
- **Idle Worker Timeout:** Worker processes may enter idle sleep after an extended period of inactivity. Background scheduled services (e.g., `SessionAutoCompletionBackgroundService`) resume execution upon the next incoming HTTP request.
- **Continuous Execution:** For mission-critical continuous background job processing without idle sleep, an active hosting tier with "Always On" enabled is recommended.
