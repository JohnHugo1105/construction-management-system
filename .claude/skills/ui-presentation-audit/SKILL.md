---
name: ui-presentation-audit
description: Audits how a UI is presented to the target end user—page complexity, information amount (too much or too little), wording (vague or too technical), visual grouping and layout fit (squashed or unreadable text, cramped or overlapping spacing, content or options cut off after an interaction, controls, options, labels, or text not completely visible where no scroll reveals the rest, content that is not viewable on screen and not reachable by scroll), and redundant controls or copy that add no value. When the user does not attach images or videos, start the local app, seed demo data if the use case needs it, and browse the named pages before judging. Also owns the full UI audit (baseline responsive audit) procedure: release lock, role/page coverage, width and engine matrix, enforced read-only, measured thresholds, proof, fix-and-release, and re-check loop. Use when the user asks for a full UI audit or baseline responsive audit, how a page feels, whether copy is clear, if a screen is overwhelming, if layout looks cramped or broken, or for a presentation, cognitive-load, or information-architecture review. For accessibility, responsive, and DOM engineering audits, also use ui-ux-pro, which must run this skill first.
---

# UI presentation audit

Audit how the screen is presented to the **target end user**. Do not start with
accessibility, viewports, or code quality. Those belong in `ui-ux-pro` after
this report.


## Invocation

- Ask to run `ui-presentation-audit` on a route, URL, or component, and name the role when known.
- Slash-style: `/ui-presentation-audit [role] <url-or-route>`

Roles: `administrator`, `project-manager`, `cost-engineer`, `site-engineer`, `hr-officer`, `warehouse-keeper`,
`accountant`, `signed-out`. If omitted, infer from the route and say so.

## When to use this vs `ui-ux-pro`

| Need | Skill |
| --- | --- |
| The five presentation questions only | This skill, stop after the report |
| Presentation plus a11y, responsive, DOM, refactor | `ui-ux-pro` (runs this skill first) |
| Full UI audit / baseline responsive audit of every side-menu page | This skill, section 5, plus `ui-ux-pro` design review |
| What the page does, not whether it is presented well | `explain-page` |

Remain read-only unless the user asked to change the UI. Then convert findings
into a precise spec for `senior_engineer`; do not redesign the page.

## 1. Establish the target user and job

Before judging the page:

- Name the target user and what they are trying to finish on this screen.
- Name the one primary outcome and the few actions or facts required for it.
- Ignore developer, platform, or internal vocabulary unless that user is the
  audience (Platform staff still get operational language, not API names).

Evaluate as a first-time user of that role, not as someone who already knows
the product.

Judge against the owner's standing principle: **show less at once, lead to one
next step** (`AGENTS.md`; `.agents/references/ui-ux-design-standards.md`,
section 1). A finding is a screen that shows everything at the same weight, an
empty tile, chip or tool shown before its moment, two places that answer the
same question differently, or a capability removed instead of re-presented.

When a surface serves more than one role (a project's budget page seen by a Cost
Engineer and a Site Engineer), audit each role separately. Roles see different controls,
and a control shown to a role that cannot use it is a finding.

State the audit scope up front and keep it honest:

- roles, environment (local port or hosted URL), and the commit being audited;
- viewports (see "State transitions and viewports");
- actions: exercise **every** function and UI-changing action that can be
  reversed without changing data (open, close, expand, filter, switch tab, open
  a sheet and dismiss it). List the actions left out because they would change
  data (send, upload, create, approve), related surfaces not covered, and states
  that could not be reached (such as failure or retry messages).

## 2. Gather presentation evidence

Judge the **rendered** screen, not the source, whenever a visual is available.

**If the user attached images or videos** of the target screens, use those as
the visual evidence. Still open any named step or state that is missing from
the attachments.

**If no images or videos were attached**, do not audit from templates alone.
Start or reuse the local app, seed only when the use case needs data, then
browse the named pages:

1. Start or reuse a healthy local ConstructOPX profile: SQL Server from `deploy/docker-compose.yml`,
   `dotnet run --project src/ConstructOPX.Api`, and `npm start --prefix web` (port 4200). Do not invent ports, launchers, or a
   parallel start path. If the user already gave a reachable local or hosted
   URL, open that after confirming it is up.
2. Seed demo data only when the target screens cannot be judged without
   realistic use-case data (an empty project list vs a project with a budget and
   costs, variance states). Use the development seeder (`Seeding:Enabled=true` in
   Development) and the smallest scenario that shows the job. Do not reset or
   destroy the local database if the current dataset already shows the required
   use case. Never seed, reset, or connect to a shared or hosted environment for
   this audit.
3. Open the browser, go to the named route or URL, and inspect the live page
   (accessibility snapshot plus screenshot). Exercise only safe,
   non-mutating interactions needed to reach the audited state (tabs,
   drawers, accordions, filters). Do not submit forms or change data.
   For a new or materially changed workflow, also open the same screens at phone width in a phone-class
   emulator or device when one is reachable, and note which one each finding came from. A finding seen only
   in the desktop browser is labelled "browser only, not a real phone".
4. Fall back to template or code inference only if the app cannot be started
   or the page cannot be opened. Set **Evidence:** to `templates/code` or
   `mixed` and say what blocked the browser.

Catalogue what the user actually meets, in scan order:

- headings, helper text, labels, badges, empty/error/loading copy
- primary vs secondary actions
- groups, cards, tabs, steppers, tables, side panels
- fields and details on the first screen vs behind a disclosure
- layout fit: clipped or squashed text, cramped gaps, elements almost
  touching or overlapping
- repeated or low-value controls, labels, helpers, badges, or values that
  say the same thing twice or add nothing to the job

### Mandatory measured-contrast scan (do not judge readability by eye)

"Barely readable" text is usually a **theme-token leak**: a dark-theme text
token (light gray such as `rgb(237,244,247)`) rendered on a white/light panel,
or the reverse. Screenshots understate it and eyeballing misses it. On every
audited state (including locked, disabled, empty, and expanded-disclosure
states), run a programmatic scan: resolve each text element's effective
background by walking up ancestors until a non-transparent `backgroundColor`,
then compute the WCAG ratio. Example page-context script:

```js
function lum(c){const m=c.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/);if(!m)return null;
  const f=v=>{v/=255;return v<=0.03928?v/12.92:Math.pow((v+0.055)/1.055,2.4);};
  return 0.2126*f(+m[1])+0.7152*f(+m[2])+0.0722*f(+m[3]);}
function ratio(fg,bg){const a=lum(fg),b=lum(bg);
  return (a&&b)?(Math.max(a,b)+0.05)/(Math.min(a,b)+0.05):null;}
function bgOf(el){let e=el;while(e){const c=getComputedStyle(e).backgroundColor;
  if(c&&c!=='rgba(0, 0, 0, 0)'&&c!=='transparent')return c;e=e.parentElement;}
  return 'rgb(255,255,255)';}
// scan main h1-h6,p,strong,small,label,dd,input,textarea; report ratio<4.5
// with {tag, ratio, color, bg, text-sample}; also note ratio<2 as "invisible".
```

Classify findings:

- **ratio < 2:1 — "invisible text" (blocking):** core content, labels, or
  entered/important values. This is a rendering defect, not a style nit; name
  the exact strings, both colors, and the leaking token.
- **ratio 2:1 – 4.5:1 (high for body/labels):** fails WCAG 2.2 AA for normal
  text; list affected components.
- Record which states are affected (editable, locked, disabled, hover) and
  whether the same surface family repeats the leak across pages.

Both polarities occur — light-theme ink on dark panels **and** dark-theme ink
on light panels — and a leak can swallow an entire surface (every heading,
label, input, and table cell on one page) while sibling pages look fine. Audit
dialogs, popovers, and drawers as their own surfaces too: a light dialog can
sit on a broken dark page (or vice versa), and auditing the page alone misses
nothing about the dialog until it is opened and measured.

Cite measured numbers (`1.04:1 light-on-light on "Information included"`) in
the report — never "looks low contrast".

### Layout-integrity probes (run alongside the contrast scan)

Add these page-context checks per viewport (1440×900, 768×1024, 375×812):

- **Whole-page reachability:** capture the complete rendered page, then actually scroll
  the page from top to bottom and back. Compare the reached scroll position with
  `scrollHeight - clientHeight`; inspect the last content and action in the live
  viewport. Repeat inside each drawer, dialog, list, or other nested scroll
  region after opening it. A full-page screenshot alone does not prove that a
  user can reach content beyond the screen or operate controls there. Check
  that fixed headers, footers, and bottom navigation do not cover the content
  reached at either end.
- **Horizontal overflow:** `document.documentElement.scrollWidth` vs
  `clientWidth` at each viewport. Report the overshoot in px; then find the
  elements whose `getBoundingClientRect().right` exceeds the client width.
- **Label–input collisions (form-grid integrity):** for every visible
  `label`/control pair and for sibling labels, compare
  `getBoundingClientRect()` intersections — a label overlapped by its own
  input, two labels colliding, or a label broken mid-word all block the form.
  Visually misaligned label→control pairing (label floating in a neighbouring
  grid column) counts as a finding even without overlap.
- **Internal identifier exposure:** regex visible text for GUID patterns
  and internal storage schemes (`r2://`, `s3://`) and flag any rendered as
  content or used as the only input format — users cannot look up or verify a
  raw GUID, so an ID-only input needs a search/picker alternative.
- **Side-sticking content:** for each panel/card, compare the first and last
  content edge against the container's border-box. Content closer than ~12px
  to a border (panels with zero or missing padding) fails — text and controls
  must not touch panel borders on any side.
- **Sticking between siblings:** measure the gap between adjacent visible
  elements and text runs (buttons, chips, rows, cards, labels, values, list
  items). A 0–4px gap with no deliberate divider or designed overlap fails —
  elements, containers, and texts must not stick together against each other
  between siblings, just as they must not touch container edges (the
  side-sticking rule above). Report the two elements and the measured gap.
- **Squashed or off-looking text:** inspect every text run in the state's
  capture: no vertically crushed or overlapping glyphs, natural line-height
  and wrapping, no mid-word breaks, no clipped ascenders or descenders, and
  no unexpectedly shrunken type. Text that reads awkwardly or "looks off" is
  a finding even when every word is technically legible.
- **Control-style inventory:** group every button by computed style
  (background, border-radius, height, weight). More than one visual treatment
  for the same role (e.g. three different secondary-button looks on one page),
  buttons below the 44px touch-target minimum, or mixed square/rounded corners
  indicate the page is not using the shared design-system classes.

- **Content pushed out of reach:** for every `overflow: hidden` ancestor of
  the audited content, a non-zero `scrollTop`/`scrollLeft` means code (such as
  `scrollIntoView` on the newest message) scrolled it and the user cannot
  scroll back. Also confirm each panel's first heading is inside its
  container's visible rect. Clipped headings or intros at the top of a
  fixed-height panel are a blocking finding.
- **Primary action reachable:** the Submit/Send/dialog-footer control is fully
  inside the viewport and not under the mobile bottom navigation, a floating
  control, or a sticky header (check `elementFromPoint` at its centre returns
  the control). Repeat with any optional panel on the page opened.
- **Complete control visibility (controls and options fully visible and
  usable):** for every interactive control on the audited state (buttons,
  links, inputs, selects, chips, tabs, expanders, row actions), compare its
  bounding box with the viewport and with each scrollable ancestor's visible
  rect. A control passes only when its complete box is visible, or scrolling
  the page or its nested region brings the whole box into view. A control or
  option that is only partly on screen or partly inside its panel, list, or
  row, where no scroll reveals the rest, is not completely visible and usable
  by a real end user: record it as a blocking finding — fixing is required,
  not a note. The reachable remainder must still meet the 44px touch target.
- **Complete label and text visibility:** a label, heading, helper line,
  value, or option text cut off by a viewport, panel, row, or control edge
  must be fixed, unless the truncation is deliberate and still usable: a
  visible ellipsis and the full text available another way (tooltip, title,
  or an expanded view), and never two truncated strings side by side. Text
  clipped mid-glyph, or cut off with no ellipsis and no way to see the rest,
  is a blocking finding — fixing is required, the same as for a control.
- **Open pickers:** open every select, dropdown, and popover on the audited
  state (Escape to close without choosing). Every option must be completely
  visible or fully reachable by scrolling the open picker's list, and long
  options must wrap rather than be cut off. An option clipped by the picker
  edge with no scroll that reveals it is a finding.
  Report the longest option and its right edge in px. Sentence-like options in
  a `<select>` are a finding even if they happen to fit.
- **Floating overlays:** intersect every `position: fixed`/`sticky` element
  (help launcher, action buttons, banners) with visible content and form
  controls. A fixed control that permanently covers table values, step labels,
  or inputs is a finding.
- **Wide tables after scrolling:** scroll each sideways-scrolling table to its
  end. The row's identifying column must still be visible, and the wrapper
  should be focusable and announced only while it overflows.
- **Chrome budget:** at 375 px, measure from the top of the viewport to the
  top of the first piece of primary content (the first message, result, or
  row). Report it in px and name what fills it. Around 190 px of chrome on an
  812 px phone is already too much.
- **Double truncation:** find neighbouring text elements on one line that are
  both truncated (`scrollWidth > clientWidth` with `text-overflow: ellipsis`),
  for example subject plus organisation in a thread header. Two cut strings side
  by side is a finding.
- **Sheets over the latest content:** open each composer sheet or menu
  (attachments, quick replies, actions) and check that it does not cover the
  newest message or the item being answered. Dismiss without choosing.
- **Muted text at small sizes:** in the contrast scan, look closely at
  secondary and muted text (timestamps, meta lines, helper text, chips) at 375
  and 768. That is where calm styling most often drops below 4.5:1.

Exercise only safe, non-mutating interactions (tabs, accordions, filters,
opening dialogs and cancelling them). Do not submit forms or change data.

### Whole-page capture and viewability defect scan (mandatory)

For **every audited state** — first load and again after each UI-changing
action:

- **Capture the whole page.** Save a full-page screenshot per viewport and
  per state, alongside the accessibility snapshot, as audit evidence (folder
  per commit, role, viewport, and state). A viewport-only screenshot of the
  first load is not sufficient evidence.
- **Flag every viewability defect.** All rendered content must be viewable:
  it either fits inside the screen or can be reached by scrolling the page or
  its nested scroll region. Anything that should be visible but cannot be
  seen is a UI defect. Flag at minimum:
  - content that should be on the screen but is not rendered where the user
    looks for it;
  - content cut off at a screen, panel, or row edge with no scroll that
    reaches it (verify the scroll actually reaches it — see the
    whole-page-reachability probe above);
  - text or elements overflowing outside the box, panel, or viewport where
    they should not;
  - content covered by another element that should not cover it (fixed or
    sticky chrome, floating controls, sibling overlap, a sheet or menu over
    the content it belongs to);
  - a control or option only partly on screen or partly inside its panel,
    list, or row, where no scroll brings its complete box into view (it
    cannot be fully seen or operated by a real end user).

  Severity is `blocking` when task content or a required control is hidden
  and `high` when secondary content is.
- **Capture after every UI change.** Any event or action that changes the UI
  in any form — open or close a dialog, drawer, dropdown, disclosure, or
  sheet; expand or collapse; select or filter; focus a field; scroll a list,
  panel, or wide table; trigger validation, loading, empty, or success
  states; switch theme — produces a screenshot of the resulting state, and
  the checks in this skill (catalogue, contrast scan, layout-integrity
  probes, text and spacing rules) are re-run on that capture before moving
  on. Auditing the first load only is a broken audit.

### State transitions and viewports

A first-load screenshot is not enough. After each safe interaction (open or
close a dialog, drawer, dropdown, or disclosure; scroll a list or table; pick an
option; focus a field; switch theme), capture a screenshot of the new state per
the mandatory capture rule above and re-run the probes on it before moving on. Cover the required viewports 375×812, 768×1024, and 1440×900.
Add 375×667 whenever the page has a fixed-height layout, dialog, drawer, sticky
footer, or composer. Add 1280×720 when the desktop layout has several panes side
by side. Use the extended set (390×844, 412×915, 430×932) for deep or
pre-release audits. A full UI audit uses the width and engine matrix in
section 5.3 instead. Judge against
`.agents/references/ui-ux-design-standards.md` sections 6 ("Content that must
stay reachable") and 7 ("Primary content first, and a benchmark to judge
against").

## 3. Answer these five questions first

Use these headings **verbatim**, in this order. Do not rename, merge, or skip
them. Every answer needs a verdict, evidence (what is on the screen), user
impact, and the smallest fix.

### 1. How complex is the page to the target end user?

Judge the number of jobs, decisions, and competing actions on this screen.

- **Simple:** one job, obvious next step, few decisions.
- **Manageable:** a few related tasks, still scannable, primary action clear.
- **Too complex:** mixed jobs, hidden or competing primary actions, nested
  exceptions, or a flow the target user cannot complete without prior knowledge.

### 2. How much information is given? is it too much or to little?

Judge against the job from step 1, not against what the system can display.

- **Too little:** the user cannot decide or act without leaving the page or
  guessing.
- **Enough:** required facts and actions are present; rare options are one step
  away.
- **Too much:** rare, advanced, or internal detail sits on the critical path and
  buries the next step.

### 3. Are the text, wordings, descriptions too vague, too technical?

Judge labels, headings, helper text, empty states, errors, and button names.

- **Clear:** first-time user of this role understands and knows what to do.
- **Too vague:** wording does not say what happened, what it means, or what to
  do next ("OK", "Submit", "Invalid input", "No data").
- **Too technical:** system, API, provider, schema, or internal names
  (`StartUtc`, status codes, permission keys, raw exceptions), internal
  storage paths (`r2://…`) rendered as content, or raw GUIDs required as the
  only way to identify a record in a form.
- **Misleading tense:** a confirmation dialog that asserts completion before
  the user acts ("Your decision and reason are saved", "Suspended …" as a
  heading on an unconfirmed action). Confirmers describe the pending action
  imperatively ("Suspend this partner?").
- **Action that isn't there:** a label or button asks the user to act when
  nothing is ready for them ("Review submitted payment evidence" before anything
  was submitted). It should read as a waiting state.
- **Inconsistent across surfaces:** a list or queue row and the record's
  detail page describe the same state differently. Open both and compare.

Cite the exact strings. Mixed pages can be clear in one place and failing in
another.

### 4. How is the design of the UI presented in terms of visually, does the elements, components are grouped and visually leads the user naturally making sense of what's the intent of the UI

Judge visual hierarchy, grouping, **and layout fit**—not color taste.

Check, on the rendered screen:

- related items sit together; scan order matches the job; one primary action
- text and content fit inside their controls or containers (not clipped,
  squashed, wrapped into unreadable stacks, or overflowing the box)
- spacing between sibling elements and within components is readable—not so
  tight that items look stuck together, colliding, or overlapping
- padding inside buttons, chips, fields, cards, and list rows leaves text
  room to breathe
- nothing is cut off or hidden after an interaction: headings at the top of
  fixed-height panels, picker options, dialog footers, and the primary action
  stay reachable, every control, label, and text run is completely visible or
  can be scrolled fully into view, and floating controls do not cover content
  (see the probes in section 2)

Verdicts:

- **Leads naturally:** related items sit together; scan order matches the job;
  one primary action; the page intent is obvious without reading everything;
  text fits its containers; spacing separates groups cleanly with no overlap.
- **Partially grouped:** some structure, but the eye still hunts, the primary
  action is weak, or isolated crowding / cramped gaps / mild clipping appear.
- **Does not communicate intent:** mixed unrelated blocks, equal visual weight,
  weak proximity, layout that contradicts the task, or widespread squashed
  text, stuck-together elements, or overlaps that obscure the job.

Also judge whether the primary content leads: it should own the canvas, with
secondary chrome behind compact rows or menus and visually quieter than the
stream. When the page follows a well-known pattern (chat, search results,
checkout), name the best-in-class products users know for it (for chat:
Messenger, WhatsApp, Viber, Instagram, Telegram). Score the screen against a few
explicit criteria, for chat: content owns the canvas, secondary chrome
disclosed, density and calm, and overall against the benchmark.

Name the affected component or region when citing fit or spacing failures.
Any measured contrast failure from the mandatory scan belongs in this answer's
evidence too — unreadable text breaks visual hierarchy no matter how good the
grouping is.

### 5. Are there redundant controls, text, or values that do not add sense or value?

Judge every visible control, label, helper, badge, status, and repeated value
against the job from step 1. Flag items that:

- duplicate the same fact, status, or action in a way that adds no new meaning
- repeat a label and a nearby value that say the same thing
- offer a second control for an action already covered by a clearer primary
- show copy, icons, or chrome that do not help the user decide or act
- leave dead or decorative controls that look interactive but do nothing useful
  for this job
- list the same requirement or fact twice under different wording (for example
  "Government-issued identity" and "At least one valid government-issued ID" in
  one checklist)

Verdicts:

- **Lean:** every visible control, text, and value earns its place for the job.
- **Some redundancy:** a few duplicates or low-value items exist; the primary
  path is still clear.
- **Heavily redundant:** repeated or pointless controls/text/values crowd the
  scan path or make the next step ambiguous.

Cite the exact strings or control labels. Prefer remove or merge over
rewriting when the content is already clear elsewhere.

## 4. Report template

Copy this structure. The five questions must appear before any optional a11y,
responsive, or engineering notes.

```markdown
# UI presentation audit — <page or route>

**Target user:** <role> (one report section per role when several share the surface)
**Job:** <what they must finish>
**Primary outcome:** <done looks like>
**Evidence:** rendered page (attached media | live browser) | templates/code | mixed
**Scope:** <environment> · commit <sha> · viewports <list> · actions exercised <list>
**Out of scope:** <data-changing actions, related surfaces, unreachable states>
**Benchmark:** <named products and the scoring criteria, when a known pattern applies>

## 1. How complex is the page to the target end user?
**Verdict:** Simple | Manageable | Too complex
**Evidence:**
**User impact:**
**Fix:**

## 2. How much information is given? is it too much or to little?
**Verdict:** Too little | Enough | Too much
**Evidence:**
**User impact:**
**Fix:**

## 3. Are the text, wordings, descriptions too vague, too technical?
**Verdict:** Clear | Too vague | Too technical | Mixed
**Evidence:** (quote the strings)
**User impact:**
**Fix:**

## 4. How is the design of the UI presented in terms of visually, does the elements, components are grouped and visually leads the user naturally making sense of what's the intent of the UI
**Verdict:** Leads naturally | Partially grouped | Does not communicate intent
**Evidence:** (include fit, spacing, overlap, and grouping)
**User impact:**
**Fix:**

## 5. Are there redundant controls, text, or values that do not add sense or value?
**Verdict:** Lean | Some redundancy | Heavily redundant
**Evidence:** (quote or name the redundant items)
**User impact:**
**Fix:**

## Recommended changes
Priority-ordered, smallest presentation fixes. Do not include unrelated visual
polish.

## Checked and not checked
**Checked:** viewports, themes, and the states/interactions exercised.
**Not checked:** anything a reader might assume was covered but was not (other
viewports, on-screen keyboard, safe areas, iOS native pickers, states that
could not be reached, other pages that reuse the component). Say why.

## Not in scope (this skill)
Accessibility, keyboard, contrast, viewports, and DOM/CSS hygiene — continue
with `ui-ux-pro` only when those were requested or that skill is already running.
Presentation-level crowding, clipped text, stuck-together or overlapping
layout, and redundant UI belong in the five questions above; engineering
root-cause (flex/grid bugs, utility-class conflicts) stays in `ui-ux-pro`.
```

Severity for each failing question: `blocking`, `high`, `medium`, or `low`,
using the same scale as `ui-ux-pro`.

When a surface is audited again on a newer commit, carry the earlier findings
forward and re-score each one on the new commit (still open, improved, fixed,
or regressed), citing both commit SHAs. Keep the evidence (screenshots and
measurements) in a folder per commit and role so the comparison can be
repeated.

## 5. Full UI audit procedure

Use this procedure when asked for a **full UI audit** or a **baseline
responsive audit** of the side-menu pages. The bar is never lowered between
rounds. This repository has no audit harness yet: drive the pages with Playwright
(pre-installed Chromium) against the local app, keep the run read-only, and
keep the script outside the repository unless the person asks for it to be
added. If a harness is added later, document it and point here.
The five questions (section 3) and the probes in section 2 still apply; this
section fixes the build, coverage, matrix, thresholds, proof, and the
fix-and-re-check loop. The by-eye design review and the standing product calls
are in `ui-ux-pro` ("Design review by eye" and "Standing product calls").

Roles: the **auditor** measures and writes up findings; the **engineer** fixes
and releases. One person or agent may hold both roles.

### 5.1 Lock the build

- Audit one named dev-local release (the current dev-local release if none is
  named). Confirm its release id at `/health/version` before capturing.
- Use a fresh browser profile/context per test account. Start every session
  from `/` and confirm the loaded scripts and stylesheets carry the release
  token. If they do not, **stop**: a cached copy gives false passes and false
  fails.

### 5.2 Coverage

- Every side-menu page for each role, signed in as the matching test account.
- In-page states: menus and drawers, sheets and pickers, tabs, projects at each
  lifecycle step, empty and filled lists, and dark mode when supported.
- Long state-specific labels (Next action and step buttons) and long names
  (project, sub-project, supplier, worker).
- A state that cannot be reached for lack of data is recorded as **"not
  verified, no data"** and is never assumed to pass. Ask the engineer for a
  dev-local-only, write-nothing test screen (see 5.9) to cover it.

### 5.3 Widths and engines

| Width × height | Engines |
| --- | --- |
| 375×812 | Chromium and WebKit |
| 393×852 (iPhone 15) | Chromium and WebKit |
| 768×1024 | Chromium |
| 1280×800 | Chromium |
| 1440×900 | Chromium |

WebKit is the iPhone Safari engine; Chrome on iPhone is also WebKit, so phone
widths must pass in WebKit. Add 375×667 for drawers, dialogs, and composers
(section 2, "State transitions and viewports").

### 5.4 Read-only, enforced

- The harness blocks every data-changing request except sign-in. Do not rely
  on care alone.
- Save, Approve, Void, Archive, Invite, and payroll-run steps are
  **measured, never pressed**.
- Never seed a shared environment to reach a state.

### 5.5 Measured checks (every page, width, and state)

The harness script (Playwright-style) opens each page at each width, engine,
and state, runs these measurements in page context, and records a flag with the
measured numbers and a capture. Flags are candidates until confirmed (5.6).

- **Visible and tappable:** each control is completely visible and usable —
  its full box on screen, or every part cut off by the viewport, panel, or
  row edge reachable by normal scroll; a control or option partly shown where
  no scroll reveals the rest fails. No sideways page scroll on phones
  (`scrollWidth > clientWidth`).
  Tap targets are **44px or more** on phones, including back links,
  expanders, and tag-buttons.
- **Spacing:** nothing touches its container border or its neighbour
  (side-sticking and sibling-gap probes in section 2); panels use the standard
  padding.
- **Text overflow:** no button, tag, chip, label, or link text wider than its
  box: `scrollWidth > clientWidth + 1`, or the text's rect extends past the
  element's rect. Truncation is allowed only with a visible ellipsis **and**
  the full text available (title, tooltip, or expanded view).
- **Text overlap:** no visible text from two different elements intersecting
  by more than **2px**. Exclude real overlays (modals, toasts) and
  screen-reader-only text.
- **Layout:** no nested scroll boxes hiding content; no ragged grids (for
  example 3+1); sticky bars and the message composer stay on screen.

### 5.6 Confirm every flag by eye

Look at every harness flag before calling it a fail. Standing exceptions that
are not findings:

- keyboard open: Save/Search may be off-screen when the focused field is
  visible and the button is reachable by scroll;
- expandable `/pricing` sections while closed;
- Messages rows while loading;
- the listing "View photo" chip;
- documented short phone captions (for example Messages, Stats, Market, Ops).

Then run the design review by eye from `ui-ux-pro`.

### 5.7 Proof and write-up

- One screenshot per finding with the problem boxed in red. The file name
  carries page, width, engine, and release id.
- Each finding records: route, width, engine, what is wrong, the measurement,
  the suggested fix, and the product call if one applies.
- Keep **one** numbered list: J-ids for the audit findings, N-ids for new
  finds during a re-check.
  - **P1:** hidden, untappable, broken, or unreachable feature.
  - **P2:** spacing or polish.
- Screenshots the user takes on a real device go straight into the list as
  findings (note their provenance).

### 5.8 Hand-off

Hand the engineer the whole list as **one batch**. No full test suite is
required before the first verify.

### 5.9 Fix and release (step 8½)

- Fix the whole numbered list in one go, directly on `main` with no pull
  requests or branches unless asked, and get the API and web builds passing.
- Measure each fix at the width and engine where it failed before calling it
  fixed. Phone fixes must also pass in WebKit. If a failure cannot be
  reproduced, say so instead of guessing.
- For states that cannot be reached without changing shared data, build a
  dev-local-only test screen that only displays the state and writes nothing
  (the gated `/dev/ui-audit` catalogue). That moves items out of "not verified,
  no data".
- Restart dev-local on the new commit and confirm `/health/version` returns
  HTTP 200 on **both** the local and the public dev-local addresses, with the
  new release id.
- Post the release id with what is in it and what is not. The re-check (5.10)
  audits that exact release.

### 5.10 Re-check loop (step 9)

- Re-check every item at its failing width and engine first, then at all
  widths.
- One screenshot per item: red box for a fail, green frame for a pass.
- Remaining fails plus new finds (N-ids) go back to the engineer as the next
  single batch.
- Repeat 5.9–5.10 until everything passes and nothing is left "not verified"
  that a test screen could cover. Then close out.

## Standards

Judge against `.agents/references/ui-ux-design-standards.md` (user's job,
progressive disclosure, microcopy, layout/hierarchy, minimalism) and
`AGENTS.md` End-User UI Language. Do not invent a new design system.
