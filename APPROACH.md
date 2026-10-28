# Assessment approach

This note records what is implemented, why that approach was chosen, what was verified, and what
remains incomplete. The code and requirements in `ASSIGNMENT_CANDIDATE.md` are authoritative.

## Task 1.1 — DEFECT-117

**Implemented in `b85ff82`.** The original ticket-list effect watched only selected filter
properties, so changing a property outside that dependency list (such as status) did not trigger a
request. Search later caused a request and exposed the stale status value.

The hook now compares a snapshot of the complete current filter set and loads when that snapshot
changes. Search is represented in the request by a separate 300 ms debounced value. This keeps
select filters immediate without sending a request for every search keystroke. The fix is in the
request flow rather than client-side filtering, forced reloads, or a remount key.

The focused Vitest regression checks the actual outgoing URL: status changes send
`status=InProgress`; search does not request before debounce; and the settled search request keeps
the selected status. Verified result: **2/2 tests passed** with
`npm run test --workspace apps/web -- src/features/tickets/hooks/useTicketList.test.ts`.

## Task 1.2 — server-side filtering

**Implemented in `88a784e`.** `TicketQueries.ApplyFilters` composes exact-match predicates for
status, priority, category, customer, assigned agent, and unassigned-only. Search trims its input,
ignores blank text, and searches ticket title, reference, or joined customer name. Predicates
combine with AND and are applied before the count, sort, and page operations, so pagination
metadata describes the filtered result set.

The filters remain in the database query rather than being applied to the materialized page in
C#. This keeps paging and totals correct. A new filter can be introduced as another predicate in
the existing helper. SQLite-backed tests cover priority, combined filters, search fields,
assigned/unassigned results, and count/page behavior.

Verified results: focused query tests **7/7 passed**; the API unit-test project **39/39 passed**.
The query tests exercise EF Core with SQLite, not a live SQL Server. No migration or index change
was required for this task.

## Task 2 — ticket escalation and assignment

### Implemented subset: BR-1–BR-3

**Implemented in `bd27a27`.** During ticket creation:

- The requested priority is used, defaulting to Medium.
- A category with `ForcesCriticalPriority` overrides the requested priority to Critical.
- The SLA due date is creation time plus the configured priority window.
- Premium customers receive the configured multiplier, with the configured minimum window.

The category behavior is driven by category flags rather than category-name checks. SLA windows
and premium adjustment values are represented by `SlaOptions` and bound from the existing `Sla`
configuration. The handler uses the injected `IClock` for creation time. Tests cover each standard
priority window, premium half-windows, forced Critical, the minimum floor, and default priority.
The complete API unit-test run after this change passed **39/39**; the presentation project build
succeeded with 0 errors and 4 NuGet vulnerability warnings.

### Incomplete requirements

BR-4 through BR-8 are **not implemented**. In particular:

- No automatic specialist-aware agent selection, open-ticket capacity check, deterministic tie
  break, or unassigned explanation (BR-4–BR-5).
- The configured at-risk threshold is not yet used to derive all SLA statuses (BR-6/BR-8).
- No escalation endpoint, escalation validation, priority transition, reassignment, or immutable
  escalation-history persistence (BR-7).
- No escalation-history API, EF migration, or database-side `slaStatus` filter.
- No complete triage-reason object in the create response.
- No React SLA filter, reusable complete SLA indicator, escalation form/history, or triage
  result display.

The existing SLA status evaluator remains limited to its current behavior; Task 2 boundary
requirements such as AtRisk and exact due-date handling are not claimed as complete.

## Task 3 — authentication

**Not implemented.** There is no login endpoint or UI, password hashing, JWT issuance/validation,
protected endpoint policy, or development login. No credentials should be supplied at submission.

## Task 4 — API Docker image

**Not implemented.** The current Compose setup is for local SQL Server only. No API Dockerfile,
container service, non-root API runtime, container health endpoint, or image verification exists.

## Setup and local environment

The committed Compose and Development configuration use SQL Server host port **1433**. A local
machine may need a different published host port if 1433 is occupied; in that case, change only
the local Compose mapping and set `ConnectionStrings__Default` in the ignored `.env` to match.
Keep the SQL Server container port at 1433. Do not treat machine-specific local overrides as
submission defaults or commit credentials.

For general run and focused test commands, see `README.md`. The reported test counts are actual
results from the focused commands listed there; the full `npm test` aggregate command was not run
for this documentation update.

## AI use and verification

AI assistance supported code inspection, implementation, focused test creation, and documentation.
Changes were checked against the assessment requirements and validated with targeted commands.
The candidate must independently understand the code and be able to explain the decisions above.
No optional SLA report or out-of-scope feature was implemented.
