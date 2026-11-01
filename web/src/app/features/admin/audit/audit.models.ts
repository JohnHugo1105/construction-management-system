/** One line of the activity log: who did what, and when. */
export interface AuditEntry {
  readonly id: string;
  readonly occurredAtUtc: string;
  readonly userName: string;
  /** A stable dotted name such as "Project.Archived". Not shown to people; the summary is. */
  readonly action: string;
  readonly entityType: string;
  readonly entityId: string;
  /** A plain sentence about what happened. */
  readonly summary: string;
}

export interface AuditListParams {
  readonly search: string;
  readonly entityType: string;
  /** The instant (ISO, UTC) the range starts, inclusive; empty for no start. */
  readonly from: string;
  /** The instant (ISO, UTC) the range ends, exclusive; empty for no end. */
  readonly to: string;
  readonly page: number;
  readonly pageSize: number;
}

/**
 * The areas the activity log can be filtered by, as the API names them and as people say them. Add a line when a feature starts
 * writing audit entries under a new entity name (for example `{ value: 'BudgetLine', label: 'Budget lines' }`).
 */
export const AUDIT_ENTITY_TYPES: readonly { readonly value: string; readonly label: string }[] = [
  { value: 'Project', label: 'Projects' },
  { value: 'SubProject', label: 'Sub-projects' },
  { value: 'User', label: 'Users' },
  { value: 'Organization', label: 'Company settings' },
];

/** The words for an entity name: known ones as people say them, unknown ones spaced out ("BudgetLine" becomes "Budget line"). */
export function entityTypeLabel(entityType: string): string {
  const known = AUDIT_ENTITY_TYPES.find((item) => item.value === entityType);
  if (known) return known.label;
  const spaced = entityType.replace(/([a-z])([A-Z])/g, '$1 $2');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}
