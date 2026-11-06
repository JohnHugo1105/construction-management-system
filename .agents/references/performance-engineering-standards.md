# Performance engineering standards

Apply these standards to every implementation in Claude Code, Codex and Cursor. Performance is part of correctness at realistic data volume;
validation remains proportionate to the affected behavior and risk.

## Default performance check (every work unit)

It applies to every work unit (feature, fix, refactor, review fix)
unless the request says to skip it, for example "no performance check".

1. **Triage, in the plan, in one line.** Does the change add or alter a read or
   write path, request, query, calculation, background job, payload, rendered
   list, page or shell load, or lock/transaction? If not (copy, styling, docs,
   configuration, comments, a rename), record "Performance: no measurable path
   touched" and measure nothing.
2. **Measure when a path is touched.** Capture the cost on the base and on the
   change with the same data: SQL statement count and time, HTTP requests per
   page load or journey, decoded response bytes, rows, rendered DOM nodes, and
   behavior under a small concurrent run where locks or transactions are
   involved. Use the cheapest run that shows it: a count or timing assertion in
   the one affected test class, the seeded volume fixture (`ConstructOPX.Infrastructure.IntegrationTests` data builders), or one local journey. Not a suite, not a load test.
3. **Keep the bound.** Turn a count or limit into a focused regression
   assertion (for example, constant SQL count as rows grow) instead of a
   one-off number.
4. **Report one line.** "Performance: <metric> before -> after on <data>", or
   "Performance: not measured; <reason>; measure with <command>". Never write
   "faster", "improved" or "no regression" without numbers: source inspection
   is a hypothesis, not a result. If this environment cannot run the needed
   measurement (no SQL Server, a blocked host), say so and give the command.
5. **The limits still hold.** A measurement never authorizes a reset, a
   speculative index or cache, a hosted deployment, or a production load test,
   and it does not widen the test posture in AGENTS.md. Small samples show
   direction, not capacity, p95 or an SLA.

## Plan and implement

- Identify the actor, expected result size, historical growth, and hottest
  reads. Use repository evidence and a populated fixture rather than assuming every project has only a few records (a large
  project holds tens of thousands of budget and cost lines).
- Keep SQL command count constant as a returned set grows. Batch supporting
  reads; avoid one database query or HTTP request per row. Do not parallelize
  operations on the same EF `DbContext` to disguise a query waterfall.
- Project only the facts a read needs. A count, summary, dashboard tile or variance report should not hydrate full
  line entities or a complete project graph. Aggregate in SQL and reuse the one authorized business calculation.
- Apply filtering, ordering, aggregation and paging in SQL where their
  semantics permit it. Preserve complete-scope metrics and historical facts
  needed for budget baselines, voided entries and time-zone boundaries; filtering those facts prematurely changes the
  result.
- Group repeated calculator inputs once and reuse lookups. Avoid scanning every budget line and cost entry again for
  each project, division or row.
- Bound initial rendering and history reads. Keep complete totals, full-set search/filtering and reachable direct-link selections. Use explicit
  paging or progressive controls instead of silently hiding actionable work.
- Keep loading/error/retry behavior clear. Do not make primary content wait
  for unrelated secondary reads. Load images and secondary histories only when
  useful, while preserving accessibility and user navigation.
- Preserve authorization, tenant filters, void and reversal rules, idempotency and client compatibility. Performance work is not
  permission to cache authority, weaken checks, truncate metrics, or reset data.
- Add indexes, caches or infrastructure only after a measured query plan,
  resource constraint or repeated cost demonstrates the need. Prefer the
  smallest change within the existing architecture.

## Verify and review

- For affected read paths, compare small and populated result sets; verify query count does not scale with rows, and
  assert output parity, scope isolation, time boundaries and relevant history rules.
- For performance-sensitive work, record relevant before/after latency,
  SQL/request count, decoded response bytes, rows and rendered DOM/card count.
  Use the same data, profile, filters and warm-up approach. Separate cold
  compilation/JIT, network/tunnel delay, streaming/materialization and browser
  rendering from the cost being improved.
- Use bounded, explicitly described concurrency. Small diagnostic samples do
  not establish throughput, p95/p99 latency, production capacity or an SLA.
  Avoid concurrent builds when comparing latency; disclose contention.
- Browse materially changed user workflows with the UI audit skills and reference viewports. Verify filtering, continuation, search, focus,
  loading, failure and retry behavior that the change affects.
- Use disposable, isolated databases for mutation-heavy regression tests. A benchmark never authorizes a reset, a
  hosted deployment or a production load test.
- State executed checks and coverage limits. Query-count tests, local database
  timing, rendered-browser evidence and hosted acceptance prove different
  things. Do not turn source inspection into a performance success claim.

Record measured evidence in the pull request or the relevant `docs/` page, with the command that produced it.
