---
name: ui-ux-pro
description: Execute a professional, technical UI/UX audit and code refactor. Always run ui-presentation-audit first (complexity, information amount, wording, visual grouping/fit, redundancy), including its live-app and optional demo-data evidence rule when no images or videos are attached, then inspect DOM, accessibility, interaction states, and responsive viewports, and finish with the by-eye design review and the standing product calls. Use when the user asks to inspect, critique, or polish any visual interface or frontend component.
---

# Professional UI/UX Inspection & Engineering Protocol

## Agent routing

Custom agents are opt-in. Do not delegate based only on this skill or UI/UX
complexity. Unless the user explicitly asks to use agents, the coordinating
parent performs the audit, permanent code changes, and validation directly. Apply
the routing recommendations below only after an explicit agent or multi-agent
request.

- Use `ui-ux-expert` as the read-only specialist for end-user presentation,
  browser, DOM, interaction, accessibility, responsive, and visual-consistency
  inspection.
- When permanent changes are requested, give its evidence and implementation
  specification to `senior-engineer`; do not ask `ui-ux-expert` to edit code.
- After implementation, use `quality-engineer` for functional and regression
  validation, and replay documented journeys with `qa-verifier`; return to
  `ui-ux-expert` only when UX or visual judgment remains.
- Add `security-engineer` only when the interface changes a meaningful identity,
  authorization, tenant, PII, payroll, upload, or other security boundary.
- Add `dev-lead-reviewer` only for broader changes where independent engineering
  review materially improves confidence.

When custom agents were explicitly requested for a small audit with no requested
implementation, use only `ui_ux_expert`.

## Evaluation baseline

Audit against `.agents/references/ui-ux-design-standards.md`, the repository's
default UI/UX standards (user-job framing, presentation, microcopy, forms and
controls, WCAG 2.2 AA, and responsive behavior). Report each finding against the
specific standard it violates so `senior_engineer` receives actionable guidance.

Every `ui-ux-pro` run must apply
`.agents/skills/ui-presentation-audit/SKILL.md` **before** DOM, accessibility,
or viewport work. Put that skill's five questions first in the report. Then
continue with sections 3–6 below. If the user asked only for presentation
(complexity, information amount, wording, visual grouping/fit, redundancy),
stop after `ui-presentation-audit` and do not expand into engineering
inspection.

## 1. Establish the user job and constraints

Before critiquing presentation or choosing a visual treatment, establish:

- the target user, context, and task they must complete;
- the observable success outcome and essential information or actions;
- the current friction or failure cause supported by browser or code evidence;
- required error recovery, accessibility, responsive, security, and product
  constraints; and
- which parts of the requested visual solution are preferences rather than
  requirements.

Derive the simplest interaction flow that satisfies the job and constraints,
then evaluate the existing interface against it. Do not use first-principles
reasoning to replace established design conventions without evidence or to turn
a focused UI correction into a redesign.

## 2. End-user presentation audit (required first)

Read and follow `.agents/skills/ui-presentation-audit/SKILL.md` in full,
including its live-app evidence rule. If the user did not attach images or
videos of the target screens, start or reuse local CMS (SQL Server from
`deploy/docker-compose.yml`, `dotnet run --project src/Cms.Api`, `npm start --prefix web`), seed demo
data through the development seeder only when the use case needs realistic data, then browse the named
pages before answering the five questions. Do not invent a seed path, reset a
healthy local database that already shows the job, or touch a shared or hosted environment.

Answer its five questions **verbatim and in order** before any DevTools,
accessibility, or viewport findings:

1. How complex is the page to the target end user?
2. How much information is given? is it too much or to little?
3. Are the text, wordings, descriptions too vague, too technical?
4. How is the design of the UI presented in terms of visually, does the elements, components are grouped and visually leads the user naturally making sense of what's the intent of the UI
5. Are there redundant controls, text, or values that do not add sense or value?

Do not skip this section because the page "looks fine" or because the request
was framed as an a11y or responsive review. Presentation failures are still
findings. Keep this section read-only until section 6.

## 3. Automated DevTools & DOM Inspection
When tasked with an audit, do not rely solely on macro visual impressions. You must actively inspect the underlying DOM structure:
- Launch the browser tool and navigate to the target page.
- Analyze the layout bounding boxes. Look for hidden overflows, broken flexbox/grid alignments, or inconsistent spacing (e.g., mixing `rem`, `em`, and `px`).
- Measure horizontal overflow per viewport (`documentElement.scrollWidth` vs `clientWidth` at 1440/768/375) and identify the elements whose right edge exceeds the viewport — a page pinned wider than the viewport fails at every size below its intrinsic width.
- Check form-grid integrity: compare `getBoundingClientRect()` of labels against their controls and sibling labels. Labels overlapped by inputs, colliding with siblings, broken mid-word, or visually unpaired from their control are blocking form defects.
- Audit dialogs, popovers, and drawers as separate surfaces — open them (cancel safely), and measure their contrast and spacing independently of the page behind them.
- Run the reachability probes from `ui-presentation-audit` (section 2): scrolled `overflow: hidden` ancestors, primary action under a fixed element, open-picker option bounds, floating overlays over content, and wide tables after scrolling. Trace each failure to its CSS cause (for example `overflow: hidden` where `clip` is needed, a flex item that cannot shrink, a picker sized by its longest option, or a global rule outranking a component's own style).
- For every viewport and UI-changing state, capture a full-page screenshot and
  inspect it; scroll the actual page and any nested panels to their ends.
  Verify that off-screen content and its controls can be reached and used.
  All rendered content must be viewable — it fits the screen or can be
  scrolled to — and anything cut off with no scroll to reach it, overflowing
  where it should not, or covered by an element that should not cover it is a
  viewability defect flagged per `ui-presentation-audit`. A screenshot showing
  all content is insufficient when scrolling is trapped or an overlay
  covers the reached content.
- Audit the styling hygiene: Check for redundant or competing utility classes (like accidental duplicate Tailwind padding classes).
- Measure the section 7 checks from the standards reference instead of judging them by eye: the chrome height above the first piece of primary content at 375 px, neighbouring strings that are both truncated, composer sheets and menus covering the latest item, and controls shown to a role that cannot use them (audit each role's view).
- Check the component stylesheet budget in the production build output (`anyComponentStyle` warns at 16 kB and errors at 20 kB). When a change would exceed it, extract the region into its own component rather than raising the global limit.

## 4. Interactive State & Accessibility (a11y) Check
A professional UI is dynamic. You must systematically test interactive states:
- **Hover/Active States:** Trigger hover states on buttons, links, and cards. Verify visual feedback is immediate and cohesive.
- **Form UX:** Click into inputs. Ensure focus rings are highly visible and that validation error states don't awkwardly jump or shift the rest of the layout.
- **Pickers and choices:** Open every select, dropdown, and popover and check its options against the viewport. Sentence-like options belong in a `.choice-list` radio group. Confirm each picker writes its own value: submit without choosing and confirm the form asks for a choice instead of sending a leftover default.
- **Scroll regions and disclosures:** A sideways-scrolling table wrapper is focusable and announced only while it overflows (`appScrollRegion`). Collapsible rows keep a visible open/closed indicator and focus outline.
- **Contrast Check:** Calculate the color contrast ratio between foreground text and backgrounds using WCAG AA guidelines. Include browser-styled parts (`::file-selector-button`, `::picker(select)`, placeholders) in both themes.

Contrast must be **measured, not eyeballed**, in every audited state
(editable, locked/disabled, empty, hover). Run the mandatory measured-contrast
scan from `.agents/skills/ui-presentation-audit/SKILL.md` (resolve each text
element's effective background by walking ancestors, compute the WCAG ratio in
page context). Treat ratio < 2:1 on core content, labels, or entered values as
an "invisible text" rendering defect — usually a dark-theme token leaking onto
a light surface or the reverse — and report the measured ratios, both colors,
and the affected states. Screenshots and visual impression alone are not
acceptable contrast evidence.

## 5. Viewport Stress-Testing
A layout that looks great on your desktop might break on other devices. Test at these explicit dimensions:
- Mobile Portrait: 375px × 812px
- Tablet: 768px × 1024px
- Desktop Standard: 1440px × 900px
- Short phone: 375px × 667px, whenever the page has a fixed-height layout, dialog, drawer, sticky footer, or composer
- Laptop: 1280px × 720px, whenever the desktop layout has several panes side by side
- Deep or pre-release audits: also 390 × 844, 412 × 915, and 430 × 932
- Full UI audit: the width and engine matrix in `ui-presentation-audit` section 5.3 (Chromium at every width, WebKit at the phone widths)

Document where elements clip, crowd, or drop awkwardly to a new line. Capture a
fresh full-page screenshot per state and re-inspect after every state change
(open/close, scroll, select, focus, theme switch), not only on first load.
Check at each size that no element, container, or text sticks to a container
edge or to a sibling, and that no text is squashed or awkward to read (the
spacing and text probes in `ui-presentation-audit`). For a new or materially changed workflow, walk it at phone width
in a phone-class emulator or device as well when one is reachable: raise the on-screen keyboard on form
fields, open native pickers, and use the back button. The browser pane stays the source for DOM, computed
styles, contrast, console and network. If no phone-class target can run, list those checks as not checked and
label the evidence "browser only, not a real phone"; never mark the phone check passed.

## Design review by eye

After the measured checks, review each page and state by eye at every width:

- **Main action obvious:** the action the user's job needs stands out; nothing
  competes with it.
- **Brand followed:** headings, colours, corner cuts, and tags use the shared
  styles; no shadows.
- **Consistent type and spacing** across pages, not only within one page.
- **Dark mode** reads cleanly (contrast still measured, per section 4).
- **Navigation:** every page is reachable from the menu at every width.
- **Copy:** full sentences, no stray spaces, no stray punctuation.

For a full UI audit (release lock, role/page coverage, width and engine matrix,
enforced read-only, measured thresholds, proof, fix-and-release, and re-check
loop), follow `.agents/skills/ui-presentation-audit/SKILL.md` section 5. This
review is its by-eye step.

## Standing product calls

Apply these decisions instead of re-raising them as open questions:

- **Long action-button labels:** prefer a short label and let the context line
  explain the rest. Action buttons wrap rather than overflow their box.
- **Phone composer tools** (attachments, quick replies, similar pickers): a
  full-screen sheet is acceptable only if picking a result closes the sheet and
  fills the message box, and the sheet's close control is at least 44px.
- **Desktop composer tools:** never a nested scroll box. Open them as a panel
  above the composer, up to about 60vh tall, with the composer always visible.

## 6. Execution & Code Delivery
Route permanent code changes to `senior_engineer`. Require it to refactor
surgically using the repository's actual Angular Material, component-style, and
design-token conventions. Do not introduce messy inline overrides, speculative
design-system abstractions, or gradient styling.

Reuse the established fixes before inventing new ones (details in
`.agents/references/ui-ux-design-standards.md` sections 6 and 7):

| Problem | Existing fix |
| --- | --- |
| Long options in a select | `.choice-list` wrapping radio rows (global style) |
| Select picker off-screen on phones | Global `appearance: base-select` rule in `web/src/styles.scss` (up to 767 px, `:where()` specificity) |
| Wide table loses row names | Sticky `th[scope='row']` plus the shared `.table-wrap` class |
| Scroll hint and tab stop on a table that fits | `appScrollRegion` directive (`web/src/app/shared/ui/scroll-region`) |
| Top of a fixed-height panel scrolled out of reach | `overflow: clip` on the panel |
| Optional content crowds a fixed-height panel | Collapsed, self-scrolling row with a sticky summary (`app-thread-documents` pattern) |
| Floating control over content | Tuck on scroll (page-help launcher pattern) |
| Browser-styled part unreadable in dark mode | Theme-aware tokens (`--theme-accent-text`, `--ink`, `--surface`) |
| Too much chrome above the primary content | Move rare controls behind a More menu or a compact disclosure |
| Two truncated strings side by side | Show the more important one in full; move the other to a second line or a detail view |
| Control shown to a role that cannot use it | Gate it by role in the template (for example an Administrator-only user invitation) |

After an app-wide style change, re-check custom-styled controls (header
workspace and branch switchers, dialogs with custom selects) at every viewport.

End the report with what was **not** checked and why, so no one assumes
coverage that did not happen. When the audit repeats on a newer commit,
re-score every earlier finding on that commit (open, improved, fixed, or
regressed) and cite both commit SHAs.
