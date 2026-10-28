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

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Read **[APPROACH.md](APPROACH.md)** for the implementation approach and current gaps.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional local overrides; never commit .env
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

The committed Compose and Development defaults use host port 1433. In Development the API applies
migrations and seeds demo data on startup. The Compose password is a throwaway local fixture;
override it in the ignored `.env` file when needed. Never commit real credentials.

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

**Port conflict:** If host port 1433 is already in use, change the host side of the SQL mapping in
your local `docker-compose.yml` and set `ConnectionStrings__Default` in your local `.env` to the
same host port. Keep the container port at 1433. These are machine-specific overrides, not the
committed default.

## Tests and verified results

```bash
npm test                      # full .NET and web test suites
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj --no-restore
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj --filter FullyQualifiedName~TicketQueriesTests --no-restore
npm run test --workspace apps/web -- src/features/tickets/hooks/useTicketList.test.ts
```

Verified during this work: the API unit-test project passed **39/39**, including the ticket-query
and ticket-creation tests; the focused SQLite query tests passed **7/7**; the focused DEFECT-117
Vitest file passed **2/2**. The Presentation project build succeeded with 0 errors and 4 existing
NuGet vulnerability warnings (`NU1902` and `NU1903`). The SQLite query tests exercise EF Core
translation against SQLite; they do not claim to be a SQL Server integration run. The full
`npm test` command was not run for this documentation update.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

### DEFECT-117 (Task 1.1)

The ticket-list request effect originally watched only selected filter fields, so changing a
filter not included in that dependency list (such as Status) did not issue a request. Search
later triggered the effect and made the earlier status appear to work. The hook now compares the
complete request-filter snapshot, requests changed filters immediately, and uses a separate
debounced search value so typing does not send a request per keystroke. The regression test
checks the actual `/api/tickets` request and query parameters, including a settled search combined
with the current status. Verify with:

```bash
npm run test --workspace apps/web -- src/features/tickets/hooks/useTicketList.test.ts
```

### Task 1.2: server-side ticket filtering

`GET /api/tickets` applies exact-match status, priority, category, customer and assigned-agent
filters, plus unassigned-only and trimmed title/reference/customer-name search. All supplied
conditions combine with AND. EF Core composes these predicates on the database query before
counting, sorting and paging, so totals and page counts describe only matching tickets. The
predicates are isolated in `TicketQueries.ApplyFilters`, making an additional filter a small
query extension rather than a rewrite.

The SQLite-backed query tests exercise EF query translation, priority filtering, combined
filters, title/reference/customer-name search, blank-search handling, assigned/unassigned
filtering, and filtered counts across pages. Run them with:

```bash
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj --filter FullyQualifiedName~TicketQueriesTests --no-restore
```

### Task 2 progress

**Partially implemented:** ticket creation applies BR-1–BR-3: requested priority defaults to Medium,
categories flagged `ForcesCriticalPriority` force Critical, and the due date is calculated from
the configured per-priority SLA window. Premium customers receive the configured multiplier with
the configured minimum-window floor. Settings are read from the existing `Sla` configuration
section. The create response includes the resulting priority, due date and derived SLA status.
Verify the focused command tests with:

```bash
dotnet test apps/api/tests/SupportDesk.UnitTests/SupportDesk.UnitTests.csproj --filter FullyQualifiedName~RaiseTicketCommandHandlerTests --no-restore
```

BR-4–BR-8 are not implemented. Assignment, escalation/history, configurable at-risk evaluation,
the complete triage explanation object, and the SLA filter remain outstanding.

### Architecture and design decisions

- The application handlers own ticket creation decisions; domain category flags carry specialist
  and forced-priority behavior without hard-coding category names in the handler.
- SLA windows are bound from the `Sla` configuration section. `IClock` supplies creation time,
  keeping time-dependent behavior testable.
- List filters are composed as `IQueryable` predicates in infrastructure and applied before
  counting and paging. The separate filter method allows later filters to be added without
  restructuring the query.
- For DEFECT-117, filtering in React, forcing a reload, or remounting the list were rejected:
  those approaches mask a missing request trigger and can conflict with server paging. The hook
  observes the complete filter snapshot, while search uses a separate debounce.
- Filtering tests use SQLite to exercise EF query translation without requiring a live SQL Server.
  Production execution is still against SQL Server.

### Authentication and development login

Authentication is **not implemented**. There is no development login or password to provide.

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with database-side `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, and `unassignedOnly` filters, plus sorting and paging. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket with BR-1–BR-3 priority and SLA calculation |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

### AI usage

AI assistance was used to inspect the existing code and assessment, implement the completed
changes, create focused tests, and draft documentation. The workflow was to trace behavior through
the existing API/domain/UI layers, make focused edits, then run targeted tests and inspect their
actual output. The `IQueryable` filter helper was retained after checking its placement before
counting/paging and passing the focused SQLite tests. An initial ticket-query test used a
lowercase customer search against case-sensitive SQLite fixture data; the assertion input was
corrected to match the fixture. A React test run also exposed spy cleanup and `act(...)`
synchronization issues, which were corrected before the focused test passed. Client-side filtering,
forced reloads, and remount keys were rejected because they would hide the request-trigger defect.
No test result is inferred from generated code; results above are from actual runs. The candidate
must review and be prepared to explain all submitted code.

## Known limitations

Task 2 is partial: BR-1–BR-3 are implemented on ticket creation; triage explanations, eligible
agent assignment, escalation, immutable escalation history, complete SLA status handling and
SLA filtering/UI remain incomplete. Task 3 authentication is not implemented. Task 4 container
packaging requirements are not implemented. Out-of-scope capabilities such as registration,
password reset, roles, OAuth/SSO, refresh tokens and MFA are not implemented.

## Submission checklist

-**Fork URL:** https://github.com/shaznamuees1-dev/northwind-support

-**Actual time spent by task (approximate):**
- Environment setup and troubleshooting: ~2 hours
- Task 1.1 — Defect fix and regression tests: ~1 hour
- Task 1.2 — Server-side filtering and tests: ~1 hour
- Task 2 — Partial implementation (BR-1–BR-3): ~1 hour
- Documentation, verification and Git: ~1 hour
- Development login: **None; authentication is not implemented.**
- Commits: DEFECT-117 `b85ff82`; Task 1.2 filtering `88a784e`; partial Task 2 BR-1–BR-3 `bd27a27`.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| SQL Server port 1433 already in use | Use the machine-specific port override described under Quick start; keep the SQL Server container's internal port at 1433. |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
