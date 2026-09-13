# InvoiceAI — Smart Invoice Digitization Platform

Conception et développement d'une plateforme intelligente de numérisation de
factures et d'extraction automatique de données par IA (OCR via l'API Gemini).

Stage — **Ahmed Zouari** · EDI Solutions · septembre 2026

---

## What it does

An accounting clerk drops a supplier invoice (PDF, JPEG or PNG) into the web app.
Within about thirty seconds the system has read it with Google's Gemini model,
extracted the invoice number, dates, supplier and customer with their tax IDs,
every line item, the VAT and the totals, and presented them next to the original
document for a human to verify and correct. Only invoices a person has confirmed
can be exported to CSV, Excel or JSON.

Manual entry typically takes three to five minutes per invoice. This reduces the
clerk's job to reviewing and confirming.

---

## Architecture

Dependencies point **inward only**:

    Api  →  Infrastructure  →  Application  →  Domain

- **Domain** — entities and enums. References nothing at all.
- **Application** — interfaces and DTOs. No frameworks, no vendor SDKs.
- **Infrastructure** — EF Core, the Gemini client, file storage, the queue.
- **Api** — controllers, authentication, CORS, the background worker.

The compiler enforces this. A controller accidentally placed in the Application
project fails to build, because that project has no reference to ASP.NET Core.

The React client lives in `web/` and is not part of the .NET solution. The two
halves communicate only over HTTP, which is why a mobile app or an ERP could
call the same API tomorrow.
### Processing pipeline

    POST /api/Invoices/upload
       validate each file (type, size, count)  → accepted[] + rejected[]
       save bytes under a GUID storage key
       insert SourceFile + Invoice (Uploaded)
       enqueue the invoice id
       → 202 Accepted, in about one second for three files

    ExtractionBackgroundService (one invoice at a time)
       dequeue → create a fresh DI scope
       Processing → call Gemini with a JSON schema → store RawExtractionJson
       success → map fields, resolve parties, replace line items → PendingReview
       failure → ExtractionFailed

    Reviewer corrects and saves → Reviewed → eligible for export

---

## Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 9, C# 13 |
| ORM | EF Core 9.0.18, SQL Server LocalDB |
| Auth | ASP.NET Core Identity + JWT bearer |
| AI | Gemini `gemini-3.8-flash` (`/v1beta/interactions`) |
| Excel | ClosedXML |
| API docs | Scalar at `/scalar` |
| Frontend | React 19, TypeScript, Vite, react-router-dom |

---

## Running it locally

Requires .NET SDK 9, SQL Server LocalDB, Node.js 20+, and a Google AI Studio
API key from https://aistudio.google.com

### Backend

    cd src/InvoiceAI.Api

    dotnet user-secrets set "ConnectionStrings:Default" "Server=(localdb)\mssqllocaldb;Database=invoiceai;Trusted_Connection=True;MultipleActiveResultSets=true"
    dotnet user-secrets set "Jwt:Key"       "at-least-32-random-characters"
    dotnet user-secrets set "Jwt:Issuer"    "InvoiceAI.Api"
    dotnet user-secrets set "Jwt:Audience"  "InvoiceAI.Client"
    dotnet user-secrets set "Gemini:ApiKey" "your-google-ai-studio-key"

    dotnet ef database update
    dotnet run

API on `https://localhost:7007`, interactive documentation at `/scalar`.
See `appsettings.Example.json` for every configuration key. **No secret is ever
committed** — they live in User Secrets, outside the repository.

### Frontend

    cd web
    npm install
    npm run dev

UI on `http://localhost:5173`.
---

## API

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/auth/register` | create an account |
| POST | `/api/auth/login` | obtain a JWT (valid 8 h) |
| GET | `/api/Invoices` | list, with optional `search`, `status`, `from`, `to`, `minTotal`, `maxTotal` |
| GET | `/api/Invoices/{id}` | summary |
| GET | `/api/Invoices/{id}/details` | full invoice with line items |
| GET | `/api/Invoices/{id}/file` | original document bytes |
| POST | `/api/Invoices/upload` | multipart batch upload → 202 Accepted |
| POST | `/api/Invoices/{id}/extract` | re-run extraction (retry) |
| PUT | `/api/Invoices/{id}` | save corrections, mark `Reviewed` |
| GET | `/api/stats` | dashboard figures and accuracy |
| GET | `/api/export?format=csv\|xlsx\|json` | export reviewed invoices |

Every route except register and login requires `Authorization: Bearer <token>`.

---

## Design decisions

**The API key never leaves the server.** It lives in User Secrets and is used only
by the backend. The browser never sees it and it is not in the repository.

**Extraction is constrained by a JSON schema**, not by hoping the model returns
valid JSON. `response_format` forces the shape; the prompt handles what a schema
cannot express — which party is the supplier, that `19%` means `19` and not `0.19`,
that ambiguous numeric dates are day-first, and above all never to invent a value
that is not printed on the document.

**Every extracted field is nullable.** `null` means "the model did not find this";
`0` means "the model found zero". Collapsing the two would hide real failures and
corrupt the accuracy metric.

**The extractor never throws.** Network errors, rate limits, timeouts and malformed
JSON all become `ExtractionResultDto(Success: false)`. A failed extraction is a
normal business outcome, not an exception.

**Uploads return 202 Accepted.** Extraction takes 15–30 seconds per document, far
too long to hold an HTTP request open. Work goes onto a bounded `Channel<int>`;
a hosted `BackgroundService` consumes it, creating a fresh DI scope per invoice
because a singleton must never capture a scoped `DbContext`.

**The database is the durable queue.** An in-memory channel does not survive a
restart, so at startup the worker re-queues every invoice still in `Uploaded` or
`Processing`. This was discovered by restarting the API mid-batch and watching the
queued work vanish.

**Rate-limit backoff is 15/30/60 seconds.** The Gemini free tier allows 20 requests
per rolling minute and asks callers to wait 16–58 s. An earlier 2/4/8 s backoff made
the problem worse, because every retry is itself a request.

**Money is `decimal(18,3)`**, never `double` — three decimals for Tunisian millimes.

**Parties are matched by tax ID first, then by name.** `SOTUMETAL SARL`,
`Sotumetal S.A.R.L.` and `SOTUMETAL` are one company; name-only matching would
fragment every per-supplier report.

**Only `Reviewed` invoices can be exported.** Unverified AI output must not reach
an accounting system.

**CSV uses semicolons and a UTF-8 BOM** so it opens correctly in Excel on a
French or Tunisian Windows installation, where the comma is the decimal separator.

**An uploaded filename never becomes a path.** Files are stored under a generated
GUID key, and `ResolveSafePath` rejects anything escaping the upload root.
---

## Measuring accuracy honestly

The dashboard compares, for every reviewed invoice, what Gemini originally proposed
(`RawExtractionJson`) against what the reviewer left in place, field by field.
A field counts only if the model proposed a value — the model is not scored for
staying silent.

The reported figure is therefore: **of the values the AI offered, what percentage
did a human keep unchanged?** The dashboard shows the sample size beside the
percentage, because a rate computed from one invoice is an anecdote, not a result.

---

## Known limitations

- **`PartyResolver` never overwrites existing party data.** It back-fills a missing
  address or tax ID but will not update one already set, so a genuine supplier
  address change does not propagate. The fix is a separate supplier master-data
  endpoint.
- **Supplier statistics are grouped in memory.** EF Core cannot translate a
  `GroupBy` over a navigation property, so the aggregate loads two columns and
  groups in C#. Correct at this scale, wrong at millions of rows — a SQL view or
  a `FromSql` aggregate is the production answer.
- **No pagination** on the invoice list.
- **TypeScript types are hand-written** to mirror the C# DTOs. They should be
  generated from the OpenAPI document the API already publishes.
- **The UI polls every three seconds** for extraction progress. WebSockets or
  Server-Sent Events would push instead.
- **Throughput is bounded by the Gemini free tier** (20 requests per minute), not
  by the architecture. A paid tier requires only a configuration change.
- **Failure reasons appear in the server log only**, not in the UI.
- **An `Admin` role exists but no endpoint uses it yet**, and no admin user is seeded.
- **Storage is local disk.** `IFileStorage` exists precisely so this can become
  Azure Blob or S3 without touching a controller.

---

## Repository layout

    src/
      InvoiceAI.Domain/          entities and enums, no dependencies
      InvoiceAI.Application/     interfaces and DTOs, no frameworks
      InvoiceAI.Infrastructure/  EF Core, Gemini client, storage, queue, processor
      InvoiceAI.Api/             controllers, auth, CORS, background worker
    tests/
      InvoiceAI.Tests/
    web/                         React + TypeScript client