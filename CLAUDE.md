@AGENTS.md

## Claude Code notes

- Skills live in `.claude/skills/` (`ui-presentation-audit`, `ui-ux-pro`). Subagents live in `.claude/agents/`.
- Delivery follows the one-pass flow in `AGENTS.md`: start from the current base branch, implement the whole scope,
  build once, run the minimum affected tests once, fix and re-run in the same pass, audit user-facing changes before
  committing, then report once with the failure log and a one-line `Performance:` result. Do not use `AskUserQuestion`
  for non-material choices; an explicit instruction in the request overrides these defaults.
- Commit task-related files only and push to the branch the task designates. Open a pull request, merge, deploy or write
  to an external system only when the request asks for it.
- Ship capabilities on, and guard what is not ready (`AGENTS.md`): a feature that depends on a service that may not be
  ready ships switched on behind one capability check (Available, Needs setup, Unavailable, Failing) that falls back to
  the manual path without losing the user's work and says so in plain words. It never turns on automatic effects that
  spend or confirm money.
- Default performance check (`AGENTS.md`): for every work unit, triage the change for a measurable read or write path,
  request, query, payload or rendered list; measure before and after only when one is touched; never claim an
  improvement without numbers. Brief subagents with it.
- Money is minor units (`long`) plus a currency code, quantities are `decimal`, financial records are voided and never
  deleted, and every query is tenant-scoped (`AGENTS.md` sections 6, 7 and 9). Treat a change to any of these as
  Complex-tier work.
- Run no background or wide test commands, and tell subagents the same.

## Model routing

Routing follows one rule: **Opus decides, Sonnet builds, Haiku extracts.** Fable is an escalation above Opus, never a
default. Each subagent sets its model and effort in its frontmatter; the Agent tool's `model` parameter overrides it for
one invocation.

| Agent | Model, effort | Why |
| --- | --- | --- |
| `solution-architect` | Opus, `xhigh` | A wrong plan costs the most |
| `dev-lead-reviewer`, `security-engineer` | Opus, `xhigh` | Independent review of material or risky changes |
| `product-planner` | Opus, `high` | Vague or contradictory requirements, backlog decomposition |
| `integration-engineer` | Opus, `high` | Reconciles parallel writers, migrations and shared surfaces |
| `ui-ux-expert`, `ux-auditor` | Opus, `high` | Screenshot and visual judgment |
| `senior-engineer` | Sonnet, `high` | Implementation from a settled plan: APIs, Angular UI, tests, routine fixes |
| `quality-engineer` | Sonnet, `high` | Test matrix from the acceptance criteria, failure classification |
| `qa-verifier` | Sonnet, `medium` | Replays documented journeys; the cheap role by design |
| `mechanical-extractor` | Haiku, `medium` | Transcription, log and diff summaries, symbol listing; read-only |

| Trigger | Agent | Skip when |
| --- | --- | --- |
| Outcome, scope, business rules or acceptance criteria are unresolved, or broad work needs decomposing | `product-planner` | Requirements are already clear and testable |
| Before any edit on Complex work: uncertain root cause, cross-component change, migration, concurrency, tenant or public-contract change | `solution-architect` | The implementation path is already clear |
| A settled plan to implement (Complex tier: `model: "opus"` for hard work) | `senior-engineer` | The main session can do Small or Normal work directly |
| Parallel writers explicitly authorized, with an ownership ledger | `integration-engineer` | Always, unless parallel writing was authorized |
| A new or materially changed user-facing workflow, or unclear interaction requirements | `ui-ux-expert` | Minor copy, styling, or a familiar component |
| Complex or multi-layer work that has passed validation, before reporting done | `dev-lead-reviewer` | Small, low-risk edits |
| A change touching authentication, authorization, tenant isolation, PII, payroll, uploads, the public API, secrets or dependencies | `security-engineer` | No security or privacy boundary changed |
| Independent acceptance testing wanted after implementation | `quality-engineer` | Focused tests already give enough confidence |
| Replaying a documented journey after a fix | `qa-verifier` | It is the original exploration |
| Mechanical transcription or extraction, no judgment | `mechanical-extractor` | Any visual, UX or review judgment is needed |

Run the reviewers (`dev-lead-reviewer`, `security-engineer`) in parallel after validation passes. Pass `model: "haiku"`
only to read-only agents. Keep Sonnet implementers off `max` effort, where they sometimes make out-of-scope changes.
Pass `model: "fable"` only after an Opus run at `xhigh` has failed on demanding reasoning, and say so in the report.
