# ConstructOPX: web client

The Angular 22 client: app shell, sign-in, the design system, Projects and Administration. Standalone components, signals,
`inject()`, functional interceptors and guards, lazy routes, OnPush everywhere. No UI framework: a small set of accessible
components in `src/app/shared/ui`, SCSS design tokens in `src/styles`, inline SVG icons. Rules live in `AGENTS.md` section 10 and
`.agents/references/ui-ux-design-standards.md`.

## Running it

| Command (from the repository root) | What it does |
| --- | --- |
| `npm ci --prefix web` | Install |
| `npm start --prefix web` | Dev server on http://localhost:4200, proxying `/api` and `/health` to `http://127.0.0.1:5100` (`web/proxy.conf.json`) |
| `npm run build --prefix web` | Production build into `web/dist/web` (budgets: initial 400 kB warning, 700 kB error) |
| `npm test --prefix web -- --watch=false` | Vitest + jsdom. One file: `-- --include src/app/features/projects/project-list-page.spec.ts` |
| `npm run check:no-gradients` and `npm run check:server-backed-interactions` | Source guards (see "Guards") |
| `npm run format --prefix web` | Prettier |

**Against the real API.** Start SQL Server (`docker compose -f deploy/docker-compose.yml up -d sql`), then
`ASPNETCORE_ENVIRONMENT=Development CONSTRUCTOPX_DATABASE_CONNECTION="<connection string>" dotnet run --project src/ConstructOPX.Api --urls http://127.0.0.1:5100`
(Development creates the schema from the model on first start). Open the dev server: on an empty database `/sign-in` offers
"Set up your company". Never put a password in a file you commit.

## Folder map

```
src/app/
  app.config.ts, app.routes.ts   providers (HTTP, router, start-up session load) and the route map
  core/                          one copy of everything the whole app shares
    api/       api-error.ts (ApiError + mapper), api-error-interceptor.ts, auth-interceptor.ts, paged-result.ts
    session/   session-state.ts (the session as signals), auth-guards.ts, permissions.ts, roles.ts, identity-api.ts
    layout/    shell (rail / bottom bar / header), user-menu, navigation.ts (the nav entries), page-title-state.ts
    notifications/  toast-service.ts
    pages/     not-found, not-allowed
  shared/                        business-agnostic only
    ui/            button, form-field (+ control, field-group), inputs (money, percent, number), dialog, confirm-dialog,
                   menu, toast-host, pager, empty-state, status-chip, skeleton, page-header, tab-nav, search-box,
                   alert, api-error-alert, icon, viewport
    forms/         server-errors.ts (map API field errors onto a form), validation-messages.ts, validators.ts
    presentation/  money.ts, percent.ts, business-time.ts, scaled-decimal.ts
    list-query/    list-query.ts (router-owned list state)
  features/<feature>/            *-api.ts (the ONLY place HttpClient is used), *-state.ts, *.models.ts, *.routes.ts, pages, specs
    auth/  projects/  admin/  dashboard/  workforce/  payroll/  inventory/  costing/   (the last five are stubs, see "Extending")
  testing/                       fixtures and DOM helpers for specs (not shipped)
src/styles/                      _tokens.scss (all colours, spacing, type, shadows; light and dark), _base, _controls, _surfaces, _overlays
```

## Route map

| Path | Needs | Page |
| --- | --- | --- |
| `/sign-in`, `/setup` | signed out; setup only while there are no users | auth pages |
| `/` | signed in | redirects to `/projects` (point it at `dashboard` when the dashboard exists) |
| `/change-password` | signed in | change password (the only open page while a new password is required) |
| `/dashboard`, `/workers`, `/payroll`, `/stock` | `costs.read`, `workforce.read`, `payroll.read`, `inventory.read` | stubs |
| `/projects`, `/projects/new`, `/projects/:id`, `/projects/:id/edit` | `projects.read`; `new` and `edit` need `projects.manage` | Projects |
| `/projects/:id/budget`, `/costs`, `/team`, `/stock` | `costs.read`, `costs.read`, `workforce.read`, `inventory.read` | project tabs (stubs) |
| `/admin/users`, `/new`, `/:id/edit` | `users.manage` | users |
| `/admin/settings` | `settings.manage` | company settings |
| `/admin/audit` | `audit.read` | activity log |
| `/not-allowed`, anything else | signed in | friendly pages |

## Extending

### Add a section (a new feature)

1. `features/<name>/<name>.routes.ts` exporting `<NAME>_ROUTES: Routes` (lazy page components via `loadComponent`).
2. `features/<name>/<name>-api.ts` with `@Injectable({ providedIn: 'root' })` and `HttpClient` calls, one method per endpoint.
   `HttpClient` may appear only in `*-api.ts` files (the guard script checks).
3. A lazy route in `app.routes.ts` with `canActivate: [permissionGuard(Permission.xRead)]` on the route that loads it, and an
   entry in `core/layout/navigation.ts` (an entry is hidden from anyone without the permission, never greyed out).
4. State: a `*-state.ts` provided on the page (`providers: [XState]`) with `injectListQuery`, `rxResource` and `computed()`;
   see `features/projects/project-list-state.ts` as the pattern.

### Stubs the next agents replace (replace the contents, keep names and paths)

| File | Export | Route |
| --- | --- | --- |
| `features/dashboard/dashboard.routes.ts` | `DASHBOARD_ROUTES` | `/dashboard` |
| `features/workforce/workforce.routes.ts` | `WORKFORCE_ROUTES` | `/workers` |
| `features/payroll/payroll.routes.ts` | `PAYROLL_ROUTES` | `/payroll` |
| `features/inventory/inventory.routes.ts` | `INVENTORY_ROUTES` | `/stock` |
| `features/costing/project-budget-tab/project-budget-tab.ts` | `ProjectBudgetTab` | `/projects/:id/budget` |
| `features/costing/project-costs-tab/project-costs-tab.ts` | `ProjectCostsTab` | `/projects/:id/costs` |
| `features/workforce/project-team-tab/project-team-tab.ts` | `ProjectTeamTab` | `/projects/:id/team` |
| `features/inventory/project-stock-tab/project-stock-tab.ts` | `ProjectStockTab` | `/projects/:id/stock` |

The section routes are loaded where `app.routes.ts` says, behind the read permission, so a `*.routes.ts` file only adds finer
guards (for example `permissionGuard(Permission.workforceManage)` on a "new worker" route). The project tab routes are in
`features/projects/projects.routes.ts` and are guarded there; a tab link is hidden from anyone who lacks the permission.

**`ProjectContext`** (`features/projects/project-context.ts`) is provided by the project detail page. A tab injects it:

```ts
private readonly context = inject(ProjectContext);
context.project()         // the loaded Project (id, name, status, subProjects, version, ...). Defined inside any tab: tabs
                          // are created only after the project has loaded.
context.projectId()  context.name()  context.subProjects()  context.activeSubProjects()
context.isArchived()      // true: the page is read-only, switch every write off
context.canManage()       // may manage projects AND the project is not archived
context.reload()          // fetch the project again (after a change that affects it)
context.replace(project)  // take the project an API call returned
```

Disable writes with `session.can('costs.write') && !context.isArchived()`. Do not render an `<app-page-header>` inside a tab:
the project page owns the title. Every project/sub-project change in the API needs the project `version`; read it from
`context.project().version` at submit time.

**Admin audit filter.** `features/admin/audit/audit.models.ts` has `AUDIT_ENTITY_TYPES`; add `{ value: 'BudgetLine', label: 'Budget lines' }`
when a feature starts writing audit entries under a new entity name.

### Money, percentages, dates (always through these)

| Helper | Use |
| --- | --- |
| `formatMoney(minor, currency, { fraction? })` | `125050, 'PHP'` gives `₱1,250.50`; two decimals always (a column lines up). `{ fraction: 'auto' }` drops `.00` for tight spaces. The currency is `session.currencyCode()`. All amounts are two-decimal minor units. |
| `parseMoneyInput(text, { allowNegative? })` | `"1,250.50"` gives `125050`; `null` for empty, `"1.234"`, `"abc"`, a misplaced comma, or anything above the safe-integer range. Reads digits, never floats. |
| `<app-money-input formControlName="x">` | Text in, minor units (or `null`) out; symbol from the session; validates and tidies on blur |
| `<app-percent-input>` / `<app-number-input [decimals]="2" unit="hours">` | Percent in, basis points out; plain decimals |
| `formatBps(1200)` / `parsePercentInput("12.5")` | `12%` / `1250` |
| `formatDate('2026-12-03')` | `3 Dec 2026` (a business date is never shifted by the browser time zone) |
| `formatDateTime(isoUtc, session.timeZoneId())` | `3 Dec 2026, 2:15 pm` |
| `todayIn(timeZone)`, `startOfDayUtc(date, timeZone)`, `addDays(date, n)` | Business "today", and a business day as exact UTC instants for API date ranges |

The client never calculates money, VAT or variance: show what the API returns.

### Forms

```html
<app-form-field label="Project name" [control]="form.controls.name" requiredMessage="Enter the project name.">
  <input appControl formControlName="name" autocomplete="off" />
</app-form-field>
```

`app-form-field` puts the label above, the hint and error below, ties them to the control (`appControl`: id, `aria-describedby`,
`aria-invalid`), and shows the error once the field was touched. `app-field-group` is the same for radios and checkboxes (a real
fieldset); use `.choice-list` and `.choice` rows. On submit: `form.markAllAsTouched()`, stop if invalid, then on an `ApiError`:
`applyServerErrors(form, error, { contractPriceMinor: 'contractPrice' })` puts each message under the right field (the API keys
errors by camelCase request field name) and returns messages for fields the form has not got; show those with
`<app-api-error-alert [error] [extra]>`. Create `injectFocusFirstInvalid()` in a field and call it with the form element so the
keyboard lands on the first problem. Examples: `project-form-page.ts`, `user-form-page.ts`, `company-settings-page.ts`.

### Router-owned list state

```ts
readonly query = injectListQuery({ search: listField.text(), status: listField.choice(['All', 'Active'], 'All') });
// query.values()  { search, status, page }      query.set({ status: 'Active' })   query.setPage(3)   query.reset()
// query.isFiltered()    A value equal to its default is not written to the address.
```

Field kinds: `listField.text()`, `choice(values, default)`, `flag()`, `integer(default, min, max)`, `date()`. Feed `query.values` to an
`rxResource` (see `project-list-state.ts`). Use `<app-search-box>` (debounced) and `<app-pager>`.

### Other building blocks

- **Dialogs.** `@if (open()) { <app-dialog title="..." (closed)="open.set(false)"> ... </app-dialog> }` (native `<dialog>`: it opens
  itself, traps focus, closes on Escape or a click outside, restores focus). Put buttons in `<div dialogActions class="dialog-actions">`.
  `app-confirm-dialog` for yes/no questions (focus starts on Cancel). A custom `role="dialog"` element is refused by the guard script.
- **Menu.** `<app-menu label="More"><button type="button" class="menu__item">...</button></app-menu>`; icon-only: `[iconOnly]="true"`
  `ariaLabel="..."` with `<app-icon menuTrigger name="more" />`.
- **Toasts.** `inject(ToastService).success('Project saved.')`. A toast reports something already done; never wire a button to a toast alone.
- **Status.** `<app-status-chip label tone icon>`; `BUDGET_HEALTH` in `status-chip.ts` holds the on track / near limit / over budget / no budget
  tone, icon and words so every screen agrees.
- **Lists.** A table at 768px and up, cards below (`Viewport.isTablet()`), `.table-wrap` for sideways scrolling with a sticky first column.
- **Session.** `session.can('x')`, `session.hasAnyPermission(...)`, `session.permit('x')` (a signal for `@if`), `session.currencyCode()`,
  `timeZoneId()`, `vatRateBps()`. Route guards: `authGuard`, `permissionGuard(...any-of)`, `adminGuard`.

## API error contract

Every failed `/api/*` call arrives in the code as an `ApiError { status, title, detail, code, errors, traceId }` (never a raw HTTP
error). `title` and `detail` are always safe to show:

| Response | What the client does |
| --- | --- |
| 422 `validation_failed` | `errors` is `{ camelCaseField: string[] }`: shown under the fields, with a short summary on long forms |
| 409 `concurrency_conflict` | "Someone else changed this" with a **Reload** button that refetches (`ApiErrorAlert` does this) |
| 409 `rule_violation` | The API's plain-words message (archived project, last administrator, duplicate sub-project name) |
| 401 while signed in | The session is cleared and the person goes to sign-in, keeping the page to return to (`reason=expired`) |
| 403 `password_change_required` | Goes to `/change-password`; the shell stays minimal until it is done |
| 5xx, network failure, a proxy page | A calm generic message plus "Reference: <trace id>". Server text and status codes are never shown |

Create and update requests carry the `version` the person opened. The API has no `ClientRequestId` on project creation, so the
form guards against double submission itself (the button is busy and ignores a second press).

## Design

Tokens in `src/styles/_tokens.scss` (brand blue, neutrals, success/warning/danger/info, budget-health colours, 4/8 spacing, type scale,
radii, shadows, a 3px focus ring); the dark theme redefines the same names under `prefers-color-scheme`. No gradients (the build guard
fails on any). Text contrast is at least 4.5:1 in both themes and was measured in the browser on every page. Touch targets are at
least 44px, focus is always visible, `prefers-reduced-motion` is respected, and colour is never the only signal (chips and alerts
have icons and words). Phone first: design at 375 x 812, then add room.

## Guards and tests

`scripts/check-no-gradients.mjs` and `scripts/check-server-backed-interactions.mjs` read `web/src/app`: `HttpClient` only in `*-api.ts`
(and `app.config.ts`, specs), no `$any(`, no action-looking button wired only to a toast, no custom `role="dialog"` without the shared
dialog, no "prototype" or mojibake in source. Specs sit next to the code and assert behaviour; `src/app/testing` has a session fixture
(`signInForTest`) and DOM helpers. `src/test-setup.ts` stands in for the native `<dialog>` methods jsdom lacks.

## Known limits

- Native date inputs follow the browser's locale for typing (for example mm/dd/yyyy); everything displayed uses "3 Dec 2026".
- Amounts assume two minor-unit decimals (centavos), as the API does for every currency.
- Checked in desktop Chromium emulating a phone, not on a real phone, iOS Safari or with a screen reader.
