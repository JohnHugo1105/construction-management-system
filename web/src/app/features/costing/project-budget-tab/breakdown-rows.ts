import { CostSummary, Figures } from '../costing.models';
import { countAttention, describeAttention } from '../health';

/** The three ways the project's money is split. */
export type BreakdownView = 'division' | 'subProject' | 'itemType';

export interface BreakdownRow {
  readonly key: string;
  readonly name: string;
  /** A quiet line under the name: the division number, a work-breakdown code, or why the row needs explaining. */
  readonly note: string;
  readonly figures: Figures;
  /** For a row that holds others: how many of them are over budget, unbudgeted or near the limit ("1 over budget"). */
  readonly attention: string;
  readonly children: readonly BreakdownRow[];
}

const UNCLASSIFIED_NOTE = 'These lines have no division yet.';
const NO_BUDGET_NOTE = 'No budget was set for this.';

function figuresOf(source: Figures): Figures {
  return {
    budgetMinor: source.budgetMinor,
    actualMinor: source.actualMinor,
    remainingMinor: source.remainingMinor,
    usedBps: source.usedBps,
    health: source.health,
  };
}

/** Plain words for a row that needs explaining: money spent where nothing was budgeted. */
function unbudgetedNote(figures: Figures): string {
  return figures.health === 'Unbudgeted' ? NO_BUDGET_NOTE : '';
}

/** The summary's rows for one view, in the order the API sent them (the biggest overrun first). */
export function breakdownRows(summary: CostSummary, view: BreakdownView): BreakdownRow[] {
  switch (view) {
    case 'division':
      return summary.byDivision.map((division) => {
        const children = division.wbs.map<BreakdownRow>((code) => ({
          key: `${division.division ?? 'none'}:${code.wbsCode ?? code.name}`,
          name: code.name,
          note: [code.wbsCode ? `Code ${code.wbsCode}` : '', unbudgetedNote(code)]
            .filter(Boolean)
            .join(' · '),
          figures: figuresOf(code),
          attention: '',
          children: [],
        }));
        return {
          key: String(division.division ?? 'none'),
          name: division.name,
          note:
            division.division === null
              ? UNCLASSIFIED_NOTE
              : [`Division ${division.division}`, unbudgetedNote(division)]
                  .filter(Boolean)
                  .join(' · '),
          figures: figuresOf(division),
          attention: describeAttention(countAttention(children.map((child) => child.figures))),
          children,
        };
      });
    case 'subProject':
      return summary.bySubProject.map((sub) => ({
        key: sub.subProjectId ?? 'whole',
        name: sub.name,
        note: unbudgetedNote(sub),
        figures: figuresOf(sub),
        attention: '',
        children: [],
      }));
    case 'itemType':
      return summary.byItemType.map((type) => ({
        key: type.itemType,
        name: type.label,
        note: unbudgetedNote(type),
        figures: figuresOf(type),
        attention: '',
        children: [],
      }));
  }
}

export interface BreakdownChoice {
  readonly value: BreakdownView;
  readonly label: string;
}

/**
 * The views worth offering. By division always; by sub-project only when the project has a real sub-project with money in it
 * (otherwise the one row would just be the whole project again); by type when there is anything to split.
 */
export function availableViews(summary: CostSummary): BreakdownChoice[] {
  const views: BreakdownChoice[] = [{ value: 'division', label: 'By division' }];
  if (summary.bySubProject.some((row) => row.subProjectId !== null))
    views.push({ value: 'subProject', label: 'By sub-project' });
  if (summary.byItemType.length > 0) views.push({ value: 'itemType', label: 'By type' });
  return views;
}

/** Whether a division starts open: yes when something inside it is over budget, unbudgeted or near its limit. */
export function startsOpen(row: BreakdownRow): boolean {
  return row.children.length > 0 && row.attention !== '';
}
