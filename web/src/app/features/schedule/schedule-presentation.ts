import { formatDate } from '../../shared/presentation/business-time';
import { currencySymbol, formatMoney } from '../../shared/presentation/money';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import {
  ScheduleActivity,
  ScheduleBudgetLine,
  ScheduleCurve,
  ScheduleCurvePoint,
} from './schedule.models';

// Words and drawing positions for what the schedule API sent. Nothing here works out a weight, a percent, a planned value or a slip:
// the server does. This file only turns those figures into sentences, and dates into positions on a drawing.

export interface ActivityStatus {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** The activity's state as a chip: words and an icon, never colour alone. The server decides "behind" and "needs lines". */
export function activityStatus(activity: ScheduleActivity): ActivityStatus {
  if (activity.needsLines) return { label: 'No budget lines', tone: 'info', icon: 'info' };
  if (activity.percentCompleteBps === 10_000)
    return { label: 'Done', tone: 'success', icon: 'check-circle' };
  if (activity.isBehind) return { label: 'Behind', tone: 'danger', icon: 'warning' };
  return { label: 'On track', tone: 'neutral', icon: 'clock' };
}

/** "3 Dec 2026 to 9 Jan 2027 · 38 days", or "3 Dec 2026 · 1 day" for a one-day activity. */
export function datesText(activity: ScheduleActivity): string {
  const days = `${activity.plannedDays} ${activity.plannedDays === 1 ? 'day' : 'days'}`;
  return activity.plannedStart === activity.plannedFinish
    ? `${formatDate(activity.plannedStart)} · ${days}`
    : `${formatDate(activity.plannedStart)} to ${formatDate(activity.plannedFinish)} · ${days}`;
}

/** How the planned finish stands against the baseline; empty without a baseline. */
export function slipText(slipDays: number | null): string {
  if (slipDays === null) return '';
  if (slipDays === 0) return 'Finishes on its baseline date';
  const days = Math.abs(slipDays);
  const unit = days === 1 ? 'day' : 'days';
  return slipDays > 0
    ? `Finishes ${days} ${unit} later than the baseline`
    : `Finishes ${days} ${unit} earlier than the baseline`;
}

/** "2 of 5 activities are behind" or empty when none is. */
export function behindText(activities: readonly ScheduleActivity[]): string {
  const behind = activities.filter((activity) => activity.isBehind).length;
  if (behind === 0) return '';
  const of = activities.length;
  return behind === 1
    ? `1 of ${of} ${of === 1 ? 'activity is' : 'activities is'} behind`
    : `${behind} of ${of} activities are behind`;
}

/** "Footings and 2 more", or "No budget lines linked". */
export function linesText(
  activity: ScheduleActivity,
  lines: ReadonlyMap<string, ScheduleBudgetLine>,
): string {
  const names = activity.budgetLineIds
    .map((id) => lines.get(id)?.description)
    .filter((name): name is string => !!name);
  if (names.length === 0) return 'No budget lines linked';
  return names.length === 1 ? names[0]! : `${names[0]} and ${names.length - 1} more`;
}

// ---- The timeline: positions on a date axis ------------------------------------------------------------------------------

const DAY_MS = 86_400_000;

/** A business date ("2026-11-02") as a day number, for positions only. */
export function dayNumber(date: string): number {
  const [year, month, day] = date.split('-').map(Number);
  return Math.round(Date.UTC(year!, (month ?? 1) - 1, day ?? 1) / DAY_MS);
}

export interface TimelineSpan {
  readonly first: number;
  /** The day after the last finish, so a one-day activity on the last day still has width. */
  readonly end: number;
}

/** The days the timeline covers: every planned and baseline date. Null without activities. */
export function timelineSpan(activities: readonly ScheduleActivity[]): TimelineSpan | null {
  if (activities.length === 0) return null;
  const starts = activities.flatMap((activity) => [
    activity.plannedStart,
    activity.baselineStart ?? activity.plannedStart,
  ]);
  const finishes = activities.flatMap((activity) => [
    activity.plannedFinish,
    activity.baselineFinish ?? activity.plannedFinish,
  ]);
  return {
    first: Math.min(...starts.map(dayNumber)),
    end: Math.max(...finishes.map(dayNumber)) + 1,
  };
}

export interface BarPosition {
  /** Percent of the track's width. */
  readonly left: number;
  readonly width: number;
}

/** Where a start-to-finish bar sits on the track, in percent; both ends are whole days. */
export function barPosition(start: string, finish: string, span: TimelineSpan): BarPosition {
  const total = Math.max(1, span.end - span.first);
  const left = ((dayNumber(start) - span.first) / total) * 100;
  const width = ((dayNumber(finish) + 1 - dayNumber(start)) / total) * 100;
  return {
    left: clampPercent(left),
    width: Math.max(0.5, Math.min(100 - clampPercent(left), width)),
  };
}

/** Where the end of today sits on the track, or null when today is outside it. */
export function todayPosition(today: string, span: TimelineSpan): number | null {
  const total = Math.max(1, span.end - span.first);
  const at = ((dayNumber(today) + 1 - span.first) / total) * 100;
  return at < 0 || at > 100 ? null : at;
}

function clampPercent(value: number): number {
  return Math.min(100, Math.max(0, value));
}

// ---- The S-curve -----------------------------------------------------------------------------------------------------------

/** The point that stands for today: the one marked today, else the last one with earned value (today after the plan). */
export function currentPoint(curve: ScheduleCurve): ScheduleCurvePoint | null {
  const marked = curve.points.find((point) => point.isToday);
  if (marked) return marked;
  const measured = curve.points.filter((point) => point.earnedMinor !== null);
  return measured.length > 0 ? measured[measured.length - 1]! : null;
}

/** The chart in one or two sentences, for everyone and especially for screen readers. Only the server's figures, in words. */
export function curveSummary(curve: ScheduleCurve, currency: string): string {
  if (curve.points.length === 0) return '';
  const point = currentPoint(curve);
  if (!point)
    return `The schedule starts on ${formatDate(curve.points[0]!.date)}. Nothing is planned before then.`;
  const money = (minor: number | null) => formatMoney(minor ?? 0, currency);
  const by = point.isToday
    ? `By today (${formatDate(point.date)})`
    : `By ${formatDate(point.date)}, the end of the schedule`;
  const baseline =
    point.baselinePlannedMinor === null
      ? ''
      : ` The baseline planned ${money(point.baselinePlannedMinor)} by then.`;
  return `${by}, ${money(point.plannedMinor)} of work was planned, ${money(point.earnedMinor)} was earned and ${money(point.actualMinor)} was spent.${baseline}`;
}

export interface CurveSeries {
  readonly key: 'planned' | 'baseline' | 'earned' | 'actual';
  readonly label: string;
  /** The SVG polyline points. */
  readonly points: string;
}

export interface CurveTick {
  readonly at: number;
  readonly label: string;
}

export interface CurveGeometry {
  readonly width: number;
  readonly height: number;
  readonly series: readonly CurveSeries[];
  readonly xTicks: readonly CurveTick[];
  readonly yTicks: readonly CurveTick[];
  /** The x of today's vertical line, or null. */
  readonly todayX: number | null;
  readonly plot: {
    readonly left: number;
    readonly top: number;
    readonly right: number;
    readonly bottom: number;
  };
}

const SERIES: readonly {
  key: CurveSeries['key'];
  label: string;
  read: (point: ScheduleCurvePoint) => number | null;
}[] = [
  { key: 'planned', label: 'Planned', read: (point) => point.plannedMinor },
  { key: 'baseline', label: 'Baseline', read: (point) => point.baselinePlannedMinor },
  { key: 'earned', label: 'Earned', read: (point) => point.earnedMinor },
  { key: 'actual', label: 'Actual cost', read: (point) => point.actualMinor },
];

/**
 * Lays the server's points out on a drawing of the given size: time across (by date, so today between two weekly points sits
 * where it belongs), money up from zero. Series with no value at a point stop there (earned and actual stop at today).
 */
export function curveGeometry(
  curve: ScheduleCurve,
  currency: string,
  width = 640,
  height = 280,
): CurveGeometry | null {
  if (curve.points.length < 2) return null;
  const narrow = width < 480;
  const plot = { left: narrow ? 52 : 72, top: 12, right: width - 16, bottom: height - 32 };
  const first = dayNumber(curve.points[0]!.date);
  const last = dayNumber(curve.points[curve.points.length - 1]!.date);
  const days = Math.max(1, last - first);
  const values = curve.points.flatMap((point) => SERIES.map((series) => series.read(point) ?? 0));
  const top = niceCeiling(Math.max(1, ...values));
  const x = (date: string) =>
    plot.left + ((dayNumber(date) - first) / days) * (plot.right - plot.left);
  const y = (minor: number) => plot.bottom - (minor / top) * (plot.bottom - plot.top);
  const round = (value: number) => Math.round(value * 10) / 10;

  const series = SERIES.map((item) => ({
    key: item.key,
    label: item.label,
    points: curve.points
      .map((point) => ({ point, value: item.read(point) }))
      .filter(
        (entry): entry is { point: ScheduleCurvePoint; value: number } => entry.value !== null,
      )
      .map((entry) => `${round(x(entry.point.date))},${round(y(entry.value))}`)
      .join(' '),
  })).filter((item) => item.points !== '');

  const xTickCount = Math.min(narrow ? 3 : 5, curve.points.length);
  const xTicks = Array.from({ length: xTickCount }, (_, index) => {
    const point =
      curve.points[Math.round((index * (curve.points.length - 1)) / Math.max(1, xTickCount - 1))]!;
    return { at: round(x(point.date)), label: shortDate(point.date) };
  });
  const yTicks = [0, 0.5, 1].map((share) => ({
    at: round(y(top * share)),
    label: compactMoney(top * share, currency),
  }));
  const today = curve.points.find((point) => point.isToday);

  return {
    width,
    height,
    series,
    xTicks,
    yTicks,
    todayX: today ? round(x(today.date)) : null,
    plot,
  };
}

/** A round number at or above the value, so the axis reads ₱2M rather than ₱1,873,412. */
function niceCeiling(value: number): number {
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const steps = [1, 2, 2.5, 5, 10];
  return (steps.find((step) => step * magnitude >= value) ?? 10) * magnitude;
}

/** "Dec 2026" for an axis label. */
function shortDate(date: string): string {
  const [, month, year] = formatDate(date).split(' ');
  return `${month} ${year}`;
}

/** "₱2.5M", "₱750K", "₱0" for an axis label. Whole pesos from minor units. */
export function compactMoney(minor: number, currency: string): string {
  const symbol = currencySymbol(currency);
  const whole = minor / 100;
  if (whole >= 1_000_000) return `${symbol}${trim(whole / 1_000_000)}M`;
  if (whole >= 1_000) return `${symbol}${trim(whole / 1_000)}K`;
  return `${symbol}${Math.round(whole)}`;
}

function trim(value: number): string {
  return (Math.round(value * 10) / 10).toString();
}
