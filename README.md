# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### DEFECT-117

The ticket request effect watched only search and pagination, so other filter state changed in the
UI without triggering a request. The hook now applies every non-search filter immediately while
debouncing only search for 300 ms, using the complete effective filter state for each request.

To verify, open the ticket list with browser DevTools on the Network tab, change any single filter,
and confirm a new `GET /api/tickets` contains that value. Type rapidly in Search and confirm only
the settled value is requested after 300 ms.

### Server-side ticket filtering

`TicketQueries.ApplyFilters` builds the ticket-list query from independent, conditional
`IQueryable` predicates for `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`,
`unassignedOnly`, and `search`. Supplied filters combine with AND; within `search`, title,
reference, and customer name combine with OR. Empty search values are ignored.

The composed query remains unmaterialized until EF Core executes `CountAsync` and the paged
`Skip`/`Take` query, so filtering occurs in SQL and both `totalCount` and `totalPages` describe the
filtered set. Relational SQLite tests, requests against the development SQL Server, and inspection
of generated SQL verified that the same predicates run before counting and pagination. A future
`slaStatus` uses the same structure with a database-translatable predicate. It derives the current
SLA state at one captured request time, including the latest escalation as the current window start.

Run the focused filtering tests with:

```bash
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj --filter FullyQualifiedName~TicketQueriesTests
```

### Automatic triage, SLA and escalation

`POST /api/tickets` delegates its decisions to reusable domain components rather than duplicating
rules in controllers. A missing requested priority defaults to Medium, while a category flagged
`ForcesCriticalPriority` overrides the request. SLA windows are Low 72h, Medium 24h, High 8h and
Critical 4h; Premium customers receive 50% with a one-hour minimum. These values and the 25%
at-risk threshold live in the validated `Sla` configuration section.

Assignment considers active agents below `MaxOpenTickets`, requires matching specialization only
when the category requires it, and selects the lowest actual open workload with Agent ID as the
deterministic tie-breaker. Resolved and Closed tickets do not count as open. If nobody qualifies,
creation still succeeds unassigned with an explanation. This calculation currently uses a
read-then-write workload snapshot: concurrent requests could choose the same agent, so production
scale would require transactional locking, optimistic concurrency, or a queue.

Escalation raises priority exactly one level, rejects Critical/Resolved/Closed tickets, recalculates
the SLA from the escalation time, and retains the agent only while eligible. Each successful change
atomically inserts an immutable `TicketEscalation` audit record. Migration
`AddTicketEscalations` adds its table, `IX_TicketEscalations_TicketId`, priority constraints, and
restricted-delete ticket/agent foreign keys so referenced audit history cannot be cascaded away.

SLA status is derived rather than stored: No SLA, Within SLA, At risk, Breached or Met. Keeping
priority, deadlines, assignment, escalation and SLA evaluation in small domain services makes the
same rules reusable by creation and escalation and independently testable. Controller-specific
logic, persisted SLA labels, client-side filtering, and per-agent workload queries were rejected
because they duplicate rules, become stale, or introduce N+1 queries. SQL-side list predicates keep
counts and pagination accurate; the React UI displays API results without recalculating them.

Task 2 is covered by domain, handler, relational query, HTTP integration and React component/hook
tests. The development SQL Server was also used to inspect generated filtering SQL and exercise
creation, SLA filtering, escalation and history. Run the complete checks with:

```bash
dotnet test
npm.cmd test --workspace apps/web
npm.cmd run typecheck --workspace apps/web
npm.cmd run lint --workspace apps/web
npm.cmd run build
```

Known limitations: assignment is not serialized across concurrent writers; SLA filtering performs
more derived SQL work than a persisted value and should be measured/indexed for large datasets;
authentication will replace the request-supplied escalation actor in Task 3.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

### Development agent credentials

Development startup gives every seeded agent a salted ASP.NET Core Identity password hash. Use a
seeded active agent email (for example `alex.turner@northwind-support.example`) with the local-only
password `NorthwindDemo1!`. Existing development agents are backfilled only when their hash is
missing, so seeding is idempotent; inactive agents receive a fixture hash but cannot authenticate.
The hash is stored on `Agent`, keeping one identity per support agent. A separate Users table was
rejected because the assignment has no non-agent accounts and it would add a redundant one-to-one
identity lifecycle. Production credentials must be provisioned separately.

JWT access tokens last 60 minutes and validate issuer, audience, signature and lifetime with no
clock-skew allowance. Before starting the API, set `Jwt__SigningKey` to a unique value of at least
32 characters (see `.env.example`); never commit a real signing key. `POST /api/auth/login` accepts
the development credentials above, and `GET /api/auth/me` returns the agent identified by the
validated token. API endpoints require JWT authorization by default; login and Swagger remain public.

The frontend stores only the access token and its expiration in `sessionStorage`: refreshes in
the same tab restore a valid session, while expired or malformed sessions are discarded; tab
closure normally ends storage. React subscribes to session changes, and the shared API client
attaches valid tokens to requests, excluding login. The server remains authoritative for JWT
validation. Unlike HttpOnly cookies, this storage is readable by JavaScript and vulnerable to
token theft through XSS. Production should use an appropriate HttpOnly/Secure/SameSite cookie
or server-managed session design with CSRF protection, alongside XSS prevention and HTTPS.

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Filtered and paged list, including `slaStatus` and the other ticket filters. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create and automatically triage a ticket |
| `POST /api/tickets/{id}/escalate` | Escalate one priority level and record audit history |
| `GET /api/tickets/{id}/escalations` | Newest-first escalation history |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
