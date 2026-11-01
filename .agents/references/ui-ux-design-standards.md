# UI/UX Design Standards

Default design guidance for every CMS user-facing change, adapted from the DriveOPX standards. `AGENTS.md` section 10
summarizes the must-haves; this reference holds the full checklist. Apply it when creating or changing Angular
components, templates, styles, UI copy, forms or navigation. The `ui-presentation-audit` skill answers the five
end-user presentation questions first. The `ui-ux-pro` skill runs that audit, then continues with accessibility,
responsive and DOM inspection against these same criteria.

The standards are drawn from established industry guidance: Nielsen's usability heuristics, WCAG 2.2 AA, Material Design
and Apple HIG, and plain-language content design. Follow the repository's existing component conventions rather than
introducing new styling systems.

## 1. Start from the user's job

Before designing or changing a screen, establish:

- the target user (Administrator, Project Manager, Cost Engineer, Site Engineer, HR Officer, Warehouse Keeper or
  Accountant) and their context (a phone on a site, a desk in the head office);
- the task they must complete and what "done" looks like to them;
- the essential information and actions for that task; and
- which parts of any requested visual treatment are preferences, not requirements.

Design the simplest flow that reaches the outcome, then build the screen around it. Do not turn a focused correction
into a redesign.

**Guiding principle: show less at once, lead to one next step.** A product is rarely doing too much; it usually shows
everything at once. Apply this to every screen:

- **Keep the capability, change the presentation.** Do not delete a feature to simplify a screen. Reorder, group, fold
  or defer it, and keep it reachable (a link, a menu entry, a "Show" disclosure with a plain label).
- **Phone first.** Design and check at 375 x 812 first; wider screens add room, not content.
- **One next step.** Lead with one job and one primary action; everything else is one step away.
- **Show a thing when its moment arrives.** No empty tiles, zero-count chips or filters, or tools for something that
  does not exist yet (variance for a project with no budget, rate history for a worker with one rate). If something is
  held back, say so in one line ("Cost breakdown appears once you add a budget line").
- **One answer in one place.** One queue or page per question; badges, counts and summaries are computed from it, so two
  places cannot disagree (the project card's "Remaining" equals the budget page's "Remaining").
- **Plain words.** The person's own words and names, not reference codes or system terms; one short notice at the point
  of action.
- **Measure it.** For a changed page, report screens of scroll (page height / 812), words, links and buttons at 375 px
  before and after, and the API requests and bytes if the data it loads changed.

Core principles to keep true on every screen:

| Principle | What it means in practice |
| --- | --- |
| Visible system status | Loading, saved, failed and pending states are always shown. |
| Real-world language | Use the user's words ("Date started"), never system names ("StartedOn"). |
| User control | Cancel, back, undo and a clear exit from every flow. |
| Consistency | Same action, same look, same label, same behavior everywhere. |
| Error prevention | Constrain inputs, disable invalid combinations, confirm destructive actions. |
| Recognition over recall | Show options and context; do not make users remember codes or earlier screens. |
| Progressive disclosure | Show what is needed now; put rare or advanced options one step away. |
| Minimalism | Every element must help the task; remove what does not. |

When inspecting or changing a user-facing page, answer these five questions first, using the `ui-presentation-audit`
skill. Keep the wording verbatim:

1. How complex is the page to the target end user?
2. How much information is given? is it too much or to little?
3. Are the text, wordings, descriptions too vague, too technical?
4. How is the design of the UI presented in terms of visually, does the elements, components are grouped and visually leads the user naturally making sense of what's the intent of the UI
5. Are there redundant controls, text, or values that do not add sense or value?

## 2. UI text (microcopy)

Text is part of the interface. Every label, heading, instruction, help message, empty state, validation message and
error must satisfy the following.

- Write for a first-time user with no technical knowledge. Short sentences, familiar words, roughly a grade 6 to 8
  reading level.
- Button labels are verbs that name the result: "Save budget line", "Record expense", "Assign worker". Never "OK",
  "Submit", "Yes".
- Be specific. "Enter a quantity greater than zero" beats "Invalid input".
- Errors state what went wrong, why when it helps, and what to do next. Never show raw API or exception text, status
  codes or internal identifiers.
  - Bad: `409 Conflict: DuplicateInvoice`
  - Good: `This supplier invoice is already recorded for this project. Open the existing entry or check the invoice number.`
- Empty states guide the user to the next action with a primary button.
  - Bad: `No budget lines found.`
  - Good: `This project has no budget yet. Add your first budget line to start tracking cost.`
- When a business or technical term is unavoidable ("VAT-exclusive amount", "variance"), explain it in one line where
  the decision is made, without dropping conditions or consequences the user needs.
- One concept, one word. Do not alternate between "expense", "cost" and "journal entry" for the same thing. One state,
  one description on every surface: a list row and the detail page describe a record's state the same way, from the
  same rule. Never offer an action when there is nothing for the user to act on yet; show a waiting state instead.
- Tone is calm, direct and respectful. No blame, no jokes in error or money contexts, no exclamation marks in system
  messages.
- Confirmations state consequences: "Void this expense? It stays on record, is removed from project totals, and cannot
  be restored."
- Labels sit above fields and stay visible; help text sits below in a lighter style; placeholders only show a format
  example ("e.g. INV-2026-0412").
- Review copy from the target user's perspective before finishing. Do not assume terms familiar to developers make sense
  to the end user.
- One time zone, one date style, one money style. Business dates are Philippine time (Asia/Manila) unless the
  organization is configured otherwise. Dates read "3 Dec 2026", never with seconds. Money reads "₱1,250,000.00"
  through the shared money formatter (`web/src/app/shared/presentation/money.ts`), never hand-formatted.

## 3. Forms, fields and controls

Forms

- Ask only for what is needed now. Every extra field lowers completion.
- Single column, logical order, related fields grouped under a heading.
- Field width hints at expected length.
- Use the right control: radio buttons for 2 to 5 exclusive options, select for longer lists, checkboxes for
  independent toggles, date picker for dates, segmented control for view modes.
- Long, sentence-like choices are wrapping radio rows (the shared `.choice-list` class), not a `<select>`: a native
  picker sizes itself to its longest option and runs off narrow screens.
- Each choice owns its value. A required choice starts empty rather than with a leftover default that silently submits
  the wrong thing.
- Mark the minority case: "(optional)" when most fields are required.
- Validate inline when the user leaves a field, re-validate on fix, and add a summary at the top on submit for long
  forms.
- Error placement: next to the field, red text plus an icon (never color alone), border change, no layout jump.
- Preserve input on error; never clear a form.
- Provide smart defaults and correct `autocomplete`, `type` and `inputmode` attributes so the right keyboard appears on
  mobile (`inputmode="decimal"` for quantities and amounts).
- Money and quantity inputs accept what people type ("1,250.50"), normalize it, and show the computed total next to the
  inputs as they type. The computed value shown is a preview; the saved value is the server's calculation.

Buttons and actions

- One primary action per view, visually dominant. Secondary actions are outlined or text buttons. Destructive actions
  look distinct and require confirmation.
- Prefer enabled buttons that explain on click over disabled buttons; if a button must be disabled, put the reason
  nearby.
- Show loading state on the button itself and prevent double submission.

Feedback and state

- Every interactive element has hover, focus, active, disabled and loading states.
- Snackbars for non-blocking confirmations; dialogs only for decisions that must block.
- Skeletons for content loading, spinners only for short indeterminate waits, progress bars when the duration is known.

Layout and hierarchy

- Consistent spacing scale (4/8 px) and type scale; build hierarchy with size, weight and spacing, not color variety.
- Group visually with proximity, cards or dividers so the eye can scan.
- Text renders without squashing: natural line-height and wrapping, no mid-word breaks, no clipped or overlapping
  glyphs, no unexpectedly shrunken type.
- Adjacent elements, containers and text runs keep visible separation on all sides.
- Limited, purposeful color: brand color, neutrals, and semantic colors for success, warning and error (under budget,
  near the limit, over budget). **No gradients and no decorative color.**
- Pair icons with labels unless the icon is universally understood (close, search).

Data display

- Tables for comparison, cards for browsing, lists for scanning.
- Right-align numbers, format dates and currency for the user's locale, show units.
- Add sort, filter and pagination when a list exceeds roughly 10 to 20 items. Large lists page on the server.

## 4. Accessibility (WCAG 2.2 AA baseline)

- Text contrast at least 4.5:1; 3:1 for large text and UI components. Verify against the **effective** background (walk
  up the ancestor chain to the first non-transparent `backgroundColor`), in every component state (editable,
  disabled, empty, expanded), measured programmatically, never by eye.
- Theme tokens must not leak across surfaces: a light-theme ink token on a dark panel (or the reverse) produces
  invisible text. A measured ratio below 2:1 on core content is a blocking rendering defect.
- Full keyboard operability, visible focus indicator, logical tab order.
- Semantic HTML and correct ARIA: labels tied to inputs, roles, live regions for asynchronous messages.
- Every form control has a programmatic label **and** a visually intact pairing: a label is never overlapped by its own
  control or a sibling, does not break mid-word, and sits in the scan position of the control it names.
- Confirmation dialogs describe the **pending** action imperatively ("Void this expense?"). Never assert completion in a
  dialog the user has not confirmed.
- Panels and cards need internal padding (16 to 24 px on every side).
- Every button uses the shared button classes for its role: consistent height (44 px minimum), radius and surface.
- Do not render internal identifiers (raw GUIDs, storage keys) as user-facing content. Where a record can only be
  identified by id, provide a searchable picker by name.
- Touch targets at least 44 x 44 px.
- Never rely on color alone; support text resize to 200% and `prefers-reduced-motion`. Working screens stay fast
  (100 to 220 ms).
- Announce errors and status changes to screen readers.

## 5. Devices and responsive behavior

- Mobile-first: design the narrowest viewport first, then add columns and density as space allows.
- Verify at least at 375 x 812 (mobile), 768 x 1024 (tablet) and 1440 x 900 (desktop). Content must reflow; no
  horizontal page scroll. Add 375 x 667 for fixed-height layouts, dialogs, drawers and sticky footers.
- Fluid layouts with relative units, flexible grids, and `min`/`max` widths.
- Touch: larger targets and spacing, tap equivalents for hover-only interactions, primary actions within thumb reach.
- Navigation adapts (bottom navigation or drawer on mobile, side or top navigation on desktop) with the same
  destinations and names.
- Mobile forms: sticky primary action, one task per screen in long flows, no tiny selects.
- Wide tables collapse to cards or scroll horizontally with a frozen key column (`th[scope='row']` is
  `position: sticky; left: 0`). A sideways-scrolling wrapper is a labelled, focusable region with a "scroll sideways"
  hint only while it actually overflows (the shared `.table-wrap` class).
- Performance is UX: lazy load, avoid layout shift.
- A desktop browser alone is "browser only, not a real phone". Say so in the report when no phone-class device or
  emulator was used.

## 6. Content that must stay reachable

These failures pass a static screenshot review and only show up after the user interacts. Design for them up front.

Viewability rule: every piece of rendered content must be viewable: it fits inside the screen or can be reached by
scrolling the page or its nested scroll region. Content that should be visible but is cut off with no scroll to reach
it, overflows where it should not, or is covered by an element that should not cover it is a UI defect (blocking when
it hides task content or a required control). Capture a full-page screenshot of every audited state and again after
every UI-changing interaction.

| Rule | What goes wrong otherwise |
| --- | --- |
| Pickers, dropdowns and popovers stay inside the viewport and wrap long options. | Options are cut off at the screen edge. |
| A fixed-height panel (drawer, dialog) has a scroll plan: one area scrolls, and the header and primary action stay put. | Content overflows the panel and is clipped with no way to reach it. |
| A clipping container that must not scroll uses `overflow: clip`, not `overflow: hidden`. | `scrollIntoView()` also scrolls `overflow: hidden` ancestors, pushing top content out of reach. |
| The primary action is always on screen and never under the bottom navigation, the on-screen keyboard, a safe area or a floating control. | The user cannot finish the task. |
| Floating controls never permanently cover content. | A table value or step label is hidden under the button. |
| Collapsible rows keep a visible open/closed indicator and focus outline. | A `display: flex` summary loses the native disclosure marker. |
| Browser-styled parts (`::file-selector-button`, placeholder) use theme-aware tokens. | Dark-theme text becomes unreadable. |
| App-wide style changes never override a control that sets its own look. | A global select rule breaks a custom control. Write global element rules with `:where()` and re-check custom controls. |

Re-inspect after every state change, not only on first load: open and close each dialog, drawer, dropdown and
disclosure; scroll lists and wide tables; select an option; trigger validation errors, loading, empty and success
states; resize between viewports.

## 7. Primary content first

Every screen has one primary content stream: the rows in a cost table, the figures on a dashboard, the lines of a
payroll run. Everything else is chrome or secondary content, and it must stay quieter than that stream.

- **Content owns the canvas.** On a phone, measure the height of headers, toolbars, banners and badges above the first
  piece of primary content. Keep that chrome budget small; about 190 px of chrome on an 812 px screen is already too
  much. Move rarely used controls behind a menu or disclosure.
- **Secondary chrome is disclosed, not displayed.** Filters, extra tools and settings sit behind a compact row, chip or
  menu with a one-line status, never as full cards on top of the stream.
- **Density and calm.** Secondary panels use lighter weight than the stream, so the eye lands on the content first. Calm
  does not mean unreadable: muted secondary text still meets 4.5:1.
- **No double truncation.** Two neighbouring strings that are both cut with an ellipsis tell the user nothing. Show the
  more important one in full and move or shorten the other.
- **Controls follow the role.** Show an action only to roles that can use it. A control that fails with a permission
  error after the click is a design defect, not a server message to word better.

Judge a pattern against best-in-class products that users already know and name them in the audit (for tabular cost
data and dashboards, leading accounting and project-management apps). The benchmark sets the bar; it is not a reason to
copy another product's branding.

## 8. Validation before completion

Match the depth to the change. For any user-facing change, confirm:

- `ui-presentation-audit` is answered first, from the live page when no images or videos were attached;
- copy reads correctly from the target user's perspective;
- interactive states, keyboard navigation and focus visibility work;
- contrast meets AA, including browser-styled parts;
- the layout holds at the reference viewports, and every section 6 rule holds after each state change;
- the primary content stream leads, and chrome and secondary panels stay quieter;
- errors, empty states and loading states are handled; and
- the report states plainly what was not checked (viewports, on-screen keyboard, safe areas, a dialog that was only
  unit-tested, other pages that reuse the changed component).

For materially changed workflows, also run the `ui-ux-pro` skill against the rendered page and include accessibility
and responsive evidence in the completion report.
