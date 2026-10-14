# Phase 1 — Universal Data Ingestion: Operations

## 1. Prerequisites

- .NET 9 SDK/runtime.
- Dedicated SQL Server database for AI Platform.
- A shared writable storage directory for API and Worker. For separate hosts, use the same mounted NAS/object-storage adapter; do not rely on each process's local relative path.
- A long random service API key stored outside source control.
- TLS and network restrictions allowing only trusted backend callers.

## 2. Database setup

1. Create an empty database, for example FVN_AI.
2. Execute SQL/AI/001_CreateAiIngestion.sql against that database.
3. Grant the AI service identity only the required DML permissions on schema ai and database connection permissions. Restrict schema modification to deployment administrators.

## 3. Configure API and Worker

Set these environment variables for both processes (use the same values for storage and SQL):

- ConnectionStrings__FvnAi = SQL Server connection string for the dedicated AI database.
- AIPlatform__StorageRoot = absolute path to a shared protected storage root.
- AIPlatform__MaximumFileSizeMb = 25 (adjust according to policy).
- AIPlatform__QueuePollSeconds = 15.

Set only on the trusted API host:

- AIPlatform__ApiKey = long random secret; do not put it in appsettings committed to Git.

The API key is a bootstrap service-to-service credential. Do not call upload endpoints directly from a browser. X-Tenant-Code and X-Actor-Id are accepted only from a trusted backend and are not a replacement for signed user delegation / FVN_REGISTER RBAC. Complete user-context delegation before broad production use.

## 4. Run locally

PowerShell example (set secrets in the current process only):

```powershell
$env:ConnectionStrings__FvnAi = "Server=localhost;Database=FVN_AI;Integrated Security=True;TrustServerCertificate=True"
$env:AIPlatform__StorageRoot = "D:\FVN_AI_DATA"
$env:AIPlatform__ApiKey = "replace-with-a-long-random-secret"
dotnet run --project FVN_REGISTER.AI.Api
```

Run the worker in a second terminal with the same database and storage variables:

```powershell
$env:ConnectionStrings__FvnAi = "Server=localhost;Database=FVN_AI;Integrated Security=True;TrustServerCertificate=True"
$env:AIPlatform__StorageRoot = "D:\FVN_AI_DATA"
dotnet run --project FVN_REGISTER.AI.Worker
```

## 5. Endpoints

- GET /health — process health.
- GET /health/ready — SQL readiness.
- GET /api/ai/capabilities — configured capabilities and upload limit.
- POST /api/ai/v1/documents — multipart upload; required headers X-AI-API-Key, X-Tenant-Code, X-Actor-Id; multipart field name file.
- GET /api/ai/v1/documents/{documentId} — required headers X-AI-API-Key and X-Tenant-Code.

Example:

```powershell
curl.exe -X POST "https://localhost:5001/api/ai/v1/documents" `
  -H "X-AI-API-Key: $env:AIPlatform__ApiKey" `
  -H "X-Tenant-Code: FCCVN" `
  -H "X-Actor-Id: FVN_REGISTER_SERVICE" `
  -F "file=@C:\Temp\sample.pdf"
```

## 6. Expected behavior

- New document: HTTP 202; document status Quarantined; job status Queued.
- Same file uploaded again by same tenant: returns existing document and does not create a second job.
- Same file uploaded under another tenant: separate metadata record.
- Unsupported extension, wrong PDF/XLSX signature or invalid UTF-8 text: HTTP 400.
- File larger than configured limit: HTTP 413.
- Missing API key configuration: upload/status routes fail closed with HTTP 503.
- Wrong/missing API key: HTTP 401.
- Document status lookup outside tenant scope: HTTP 404.

## 7. Storage and security operations

- Storage root must be outside web root, protected by OS ACLs and disk encryption.
- Back up SQL metadata and original objects together; test restore and retention/deletion procedures.
- Monitor free disk space, SQL availability, upload failures, queue depth and duplicate rates.
- New files stay Quarantined. Do not manually change status to ReadyForProcessing until malware scanning and release policy are deployed.
- Do not expose the API publicly or enable cloud AI providers during this phase.
