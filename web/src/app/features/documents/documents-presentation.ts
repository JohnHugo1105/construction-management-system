import { formatDate, todayIn } from '../../shared/presentation/business-time';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import {
  DocumentCategory,
  DocumentListItem,
  DocumentStatus,
  DocumentVersion,
  ProjectDocument,
} from './documents.models';

// How documents are put into words. Pure functions, so the tab, the dialogs and the versions list say the same thing. The server
// decides the expiry state and the days left; nothing here works a date out.

export interface ChipLook {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** The kinds in the order the server lists them, with the words people use. */
export const CATEGORY_CHOICES: readonly {
  readonly value: DocumentCategory;
  readonly label: string;
  readonly heading: string;
}[] = [
  { value: 'Contract', label: 'Contract', heading: 'Contracts' },
  { value: 'Drawing', label: 'Drawing', heading: 'Drawings' },
  { value: 'Permit', label: 'Permit', heading: 'Permits' },
  { value: 'Insurance', label: 'Insurance policy', heading: 'Insurance' },
  { value: 'Certificate', label: 'Certificate', heading: 'Certificates' },
  { value: 'Photo', label: 'Photo', heading: 'Photos' },
  { value: 'Report', label: 'Report', heading: 'Reports' },
  { value: 'Other', label: 'Other document', heading: 'Other documents' },
];

/** Contract and Insurance reveal prices: only people who can see costs see or add them. */
export const PRICED_CATEGORIES: readonly DocumentCategory[] = ['Contract', 'Insurance'];

/** The kinds that carry a valid-until date. */
export const DATED_CATEGORIES: readonly DocumentCategory[] = ['Permit', 'Insurance', 'Certificate'];

export function isDated(category: DocumentCategory): boolean {
  return DATED_CATEGORIES.includes(category);
}

/** The kinds a person may pick: without costs.read, Contract and Insurance are not offered. */
export function categoryChoices(canSeePriced: boolean) {
  return CATEGORY_CHOICES.filter(
    (choice) => canSeePriced || !PRICED_CATEGORIES.includes(choice.value),
  );
}

export function categoryLabel(category: DocumentCategory): string {
  return CATEGORY_CHOICES.find((choice) => choice.value === category)?.label ?? 'Document';
}

/** "Expires in 12 days", "Expires tomorrow", "Expires today". */
export function expiresIn(daysLeft: number): string {
  if (daysLeft === 0) return 'Expires today';
  if (daysLeft === 1) return 'Expires tomorrow';
  return `Expires in ${daysLeft} days`;
}

/**
 * The chip a row shows, or null when there is nothing to say: Voided; Expired; "Expires in N days" within 30 days; "Valid until 3 Dec
 * 2027" for a dated document further off. An undated Active document has no chip (its kind is already its group's heading).
 */
export function documentChip(
  document: Pick<DocumentListItem, 'status' | 'expiry' | 'daysLeft' | 'validUntil'>,
): ChipLook | null {
  if (document.status === 'Void') return { label: 'Voided', tone: 'neutral', icon: 'minus-circle' };
  if (document.expiry === 'Expired') return { label: 'Expired', tone: 'danger', icon: 'error' };
  if (document.expiry === 'ExpiringSoon' && document.daysLeft !== null)
    return { label: expiresIn(document.daysLeft), tone: 'warning', icon: 'clock' };
  if (document.expiry === 'Valid' && document.validUntil)
    return {
      label: `Valid until ${formatDate(document.validUntil)}`,
      tone: 'success',
      icon: 'check-circle',
    };
  return null;
}

/** "1.2 MB", "350 KB", "800 bytes". */
export function sizeWords(bytes: number | null): string {
  if (bytes === null) return '';
  if (bytes < 1024) return `${bytes} bytes`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  const megabytes = bytes / (1024 * 1024);
  return `${megabytes >= 10 ? Math.round(megabytes) : Math.round(megabytes * 10) / 10} MB`;
}

/** The accepted types in words, every one named: "PDF, JPG or PNG". */
export function allowedTypesWords(extensions: readonly string[]): string {
  const upper = extensions.map((extension) => extension.toUpperCase());
  if (upper.length <= 1) return upper.join('');
  return `${upper.slice(0, -1).join(', ')} or ${upper[upper.length - 1]}`;
}

/** The lower-case extension of a chosen file ("pdf"), or an empty string. */
export function extensionOf(fileName: string): string {
  const dot = fileName.lastIndexOf('.');
  return dot < 0 || dot === fileName.length - 1 ? '' : fileName.slice(dot + 1).toLowerCase();
}

/**
 * Checks a chosen file against what the server said it accepts, so a person hears about a wrong type or a big file before waiting
 * for an upload. The server checks again, and it alone looks inside the file.
 */
export function fileProblem(
  file: File,
  maximumBytes: number,
  allowedExtensions: readonly string[],
): string | null {
  if (!allowedExtensions.includes(extensionOf(file.name)))
    return `This type of file can't be added. Use ${allowedTypesWords(allowedExtensions)}.`;
  if (file.size === 0) return 'The file is empty. Choose the file again.';
  if (file.size > maximumBytes)
    return `The file is larger than ${sizeWords(maximumBytes)}. Make it smaller, or add a link to where it is kept.`;
  return null;
}

/** What a version is, in one line: "Version 2 · PDF, 1.2 MB · Ana Reyes, 3 Dec 2026" or "Version 1 · Link · …". */
export function versionLine(version: DocumentVersion, timeZone: string): string {
  const what =
    version.kind === 'Link'
      ? 'Link'
      : [extensionOf(version.fileName ?? '').toUpperCase(), sizeWords(version.sizeBytes)]
          .filter(Boolean)
          .join(', ');
  const day = formatDate(todayIn(timeZone, new Date(version.addedAtUtc)));
  const who = version.addedByName ?? 'Someone';
  return [`Version ${version.number}`, what, `${who}, ${day}`].filter(Boolean).join(' · ');
}

/** The words on a version's open button: a file downloads, a link opens where it is kept. */
export function openWords(version: Pick<DocumentVersion, 'kind'>): string {
  return version.kind === 'Link' ? 'Open link' : 'Download';
}

// ---- What a person can do ------------------------------------------------------------------------------------------------------

export interface DocumentActions {
  /** Add a newer file or link. */
  readonly addVersion: boolean;
  /** Change the title, kind, description or valid-until date. */
  readonly edit: boolean;
  /** Mark it as no longer in use, with a reason. */
  readonly voidDocument: boolean;
}

/** Which changes are offered. A reader, an archived project and a voided document get no buttons they cannot use. */
export function documentActions(
  document: { readonly status: DocumentStatus },
  canManage: boolean,
  projectArchived: boolean,
): DocumentActions {
  const open = canManage && !projectArchived && document.status === 'Active';
  return { addVersion: open, edit: open, voidDocument: open };
}

// ---- Grouping ------------------------------------------------------------------------------------------------------------------

export interface DocumentGroup {
  readonly key: string;
  readonly heading: string;
  readonly items: readonly DocumentListItem[];
}

/**
 * The page's documents grouped as the server ordered them: Active ones by kind, then every voided one together at the end. Only kinds
 * that have a document on this page get a heading.
 */
export function groupDocuments(items: readonly DocumentListItem[]): DocumentGroup[] {
  const groups: DocumentGroup[] = [];
  for (const choice of CATEGORY_CHOICES) {
    const inKind = items.filter((item) => item.status === 'Active' && item.category === choice.value);
    if (inKind.length > 0) groups.push({ key: choice.value, heading: choice.heading, items: inKind });
  }
  const voided = items.filter((item) => item.status === 'Void');
  if (voided.length > 0) groups.push({ key: 'void', heading: 'Voided', items: voided });
  return groups;
}

/** The words for a voided document's state on its versions list. */
export function voidedWords(
  document: Pick<ProjectDocument, 'voidReason' | 'voidedByName' | 'voidedAtUtc'>,
  timeZone: string,
): string {
  const who = document.voidedByName ?? 'Someone';
  const day = document.voidedAtUtc
    ? formatDate(todayIn(timeZone, new Date(document.voidedAtUtc)))
    : '';
  const reason = document.voidReason ? `: ${document.voidReason}` : '';
  return `Voided by ${who}${day ? ` on ${day}` : ''}${reason}`;
}
