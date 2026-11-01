import { formatBpsInput, parsePercentInput } from '../../shared/presentation/percent';
import { ProgressLine, RecordProgressRequest } from './progress.models';

/** One step of the quick stepper: 5 percentage points. */
export const STEP_BPS = 500;
const FULL_BPS = 10_000;

/** What a person has typed for one line and not saved yet. The text is kept as typed until the field is left. */
export interface LineDraft {
  readonly text: string;
  readonly note: string;
}

/** What the server said about one line of a refused update. */
export interface LineProblems {
  readonly percent?: string;
  readonly note?: string;
  readonly line?: string;
}

/** The typed percent in basis points, or null when it is empty, not a number, or outside 0 to 100. */
export function draftBps(draft: LineDraft): number | null {
  const bps = parsePercentInput(draft.text);
  return bps !== null && bps >= 0 && bps <= FULL_BPS ? bps : null;
}

/** A draft counts when it differs from what is saved, or when the typed text is not a percent yet (so it gets fixed, not lost). */
export function isChanged(line: ProgressLine, draft: LineDraft | undefined): boolean {
  if (!draft) return false;
  const bps = draftBps(draft);
  return bps === null || bps !== line.percentBps;
}

export function percentProblem(draft: LineDraft | undefined): string | null {
  return draft && draftBps(draft) === null ? 'Enter a percent between 0 and 100.' : null;
}

/** The note box appears only when the person lowers a figure, because a lower figure needs its reason (the server enforces it). */
export function isLowering(line: ProgressLine, draft: LineDraft | undefined): boolean {
  if (!draft) return false;
  const bps = draftBps(draft);
  return bps !== null && bps < line.percentBps;
}

/** The text after pressing the stepper: moves from what is typed (or saved) by whole steps and stays between 0 and 100. */
export function steppedText(line: ProgressLine, draft: LineDraft | undefined, direction: 1 | -1): string {
  const current = (draft ? draftBps(draft) : null) ?? line.percentBps;
  const moved = Math.min(FULL_BPS, Math.max(0, current + direction * STEP_BPS));
  return formatBpsInput(moved);
}

/** The text a line's field shows: what was typed, else the saved percent. */
export function fieldText(line: ProgressLine, draft: LineDraft | undefined): string {
  return draft ? draft.text : formatBpsInput(line.percentBps);
}

/**
 * The batch to send: every changed line with a valid percent, in the order the lines are shown, each carrying the percent the
 * person was looking at. Null when nothing changed.
 */
export function buildRequest(
  lines: readonly ProgressLine[],
  drafts: ReadonlyMap<string, LineDraft>,
  asOfDate: string,
  clientRequestId: string,
): RecordProgressRequest | null {
  const items = lines.flatMap((line) => {
    const draft = drafts.get(line.budgetLineId);
    const bps = draft ? draftBps(draft) : null;
    if (!draft || bps === null || bps === line.percentBps) return [];
    const note = draft.note.trim();
    return [
      {
        budgetLineId: line.budgetLineId,
        percentBps: bps,
        note: note === '' ? null : note,
        expectedPercentBps: line.percentBps,
      },
    ];
  });
  return items.length === 0 ? null : { asOfDate, lines: items, clientRequestId };
}

const LINE_ERROR = /^lines\[(\d+)\]\.(percentBps|note|budgetLineId)$/;

/**
 * Sorts the server's per-field messages onto the lines they belong to. The server keys them by the position of the line in the
 * request (`lines[1].note`); `order` is the budget line ids in the order they were sent. Everything else (the date, the batch as
 * a whole) comes back as `form`.
 */
export function placeServerErrors(
  errors: Readonly<Record<string, readonly string[]>>,
  order: readonly string[],
): { readonly lines: ReadonlyMap<string, LineProblems>; readonly form: readonly string[] } {
  const lines = new Map<string, LineProblems>();
  const form: string[] = [];
  for (const [field, messages] of Object.entries(errors)) {
    const match = LINE_ERROR.exec(field);
    const id = match ? order[Number(match[1])] : undefined;
    if (!match || id === undefined) {
      form.push(...messages);
      continue;
    }
    const key = match[2] === 'percentBps' ? 'percent' : match[2] === 'note' ? 'note' : 'line';
    lines.set(id, { ...lines.get(id), [key]: messages.join(' ') });
  }
  return { lines, form };
}
