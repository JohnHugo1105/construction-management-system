import { Params } from '@angular/router';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import {
  AppNotification,
  Colleague,
  NotificationKind,
  RecordKind,
  TaskListItem,
  TaskStatus,
} from './collaboration.models';

export interface Look {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** A task's state as words and an icon, never colour alone. An Open task past its due date says "Overdue". */
export function taskLook(task: Pick<TaskListItem, 'status' | 'isOverdue'>): Look {
  if (task.status === 'Done') return { label: 'Done', tone: 'success', icon: 'check-circle' };
  if (task.status === 'Cancelled')
    return { label: 'Cancelled', tone: 'neutral', icon: 'minus-circle' };
  return task.isOverdue
    ? { label: 'Overdue', tone: 'danger', icon: 'clock' }
    : { label: 'Open', tone: 'info', icon: 'clock' };
}

export const STATUS_CHOICES: readonly {
  readonly value: '' | TaskStatus;
  readonly label: string;
}[] = [
  { value: 'Open', label: 'Open' },
  { value: 'Done', label: 'Done' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: '', label: 'Every state' },
];

export interface RecordLink {
  readonly link: readonly string[];
  readonly queryParams: Params;
}

/**
 * The page a record opens on. The kind and the ids the server sent decide it, never a route from the server. A change order opens on its
 * project's Changes tab; it needs the project.
 */
export function recordLink(kind: RecordKind, id: string, projectId: string | null): RecordLink {
  switch (kind) {
    case 'Task':
      return { link: ['/tasks', id], queryParams: {} };
    case 'Project':
      return { link: ['/projects', id], queryParams: {} };
    case 'PurchaseOrder':
      return { link: ['/purchasing/orders', id], queryParams: {} };
    case 'ChangeOrder':
      return projectId
        ? { link: ['/projects', projectId, 'changes'], queryParams: { order: id } }
        : { link: ['/projects'], queryParams: {} };
    case 'ClientBill':
      return { link: ['/billing/bills', id], queryParams: {} };
    case 'Subcontract':
      return { link: ['/subcontracts', id], queryParams: {} };
    case 'Bond':
      return { link: ['/bonds', id], queryParams: {} };
  }
}

export function notificationLink(notification: AppNotification): RecordLink {
  return recordLink(notification.targetKind, notification.targetId, notification.projectId);
}

const NOTIFICATION_ICONS: Readonly<Record<NotificationKind, IconName>> = {
  TaskAssigned: 'check',
  TaskDone: 'check-circle',
  Mention: 'message',
  DraftDecided: 'contract',
};

export function notificationIcon(kind: NotificationKind): IconName {
  return NOTIFICATION_ICONS[kind];
}

/** The bell's text: nothing at 0, the number up to 99, then "99+". */
export function unreadBadge(unread: number): string {
  if (unread <= 0) return '';
  return unread > 99 ? '99+' : String(unread);
}

// ---- Mentions in the comment box --------------------------------------------------------------------------------------

/** The "@" word the caret is in, if any: where it starts and what has been typed after the "@" so far. */
export interface MentionQuery {
  readonly start: number;
  readonly search: string;
}

/**
 * The name being typed after an "@" just before the caret: "@pi" gives "pi". An "@" inside a word (an email address) is not a mention,
 * and a mention ends at a line break or after 40 characters. Names have spaces, so a space does not end the search.
 */
export function mentionQueryAt(text: string, caret: number): MentionQuery | null {
  const before = text.slice(0, caret);
  const at = before.lastIndexOf('@');
  if (at < 0) return null;
  if (at > 0 && !/\s/.test(before[at - 1] ?? '')) return null;
  const search = before.slice(at + 1);
  if (search.length > 40 || /[\n@]/.test(search) || search.startsWith(' ')) return null;
  return { start: at, search };
}

/** Puts "@Name " in place of the "@" word being typed, and says where the caret goes next. */
export function insertMention(
  text: string,
  query: MentionQuery,
  caret: number,
  name: string,
): { readonly text: string; readonly caret: number } {
  const rest = text.slice(caret);
  // One space after the name: add it unless the words already go on with one, and put the caret after it either way.
  const inserted = /^\s/.test(rest) ? `@${name}` : `@${name} `;
  return {
    text: text.slice(0, query.start) + inserted + rest,
    caret: query.start + `@${name} `.length,
  };
}

/** The people picked from the list whose "@Name" is still in the words: deleting the name takes the mention away too. */
export function mentionsStillIn(text: string, picked: readonly Colleague[]): Colleague[] {
  const seen = new Set<string>();
  return picked.filter((person) => {
    if (seen.has(person.id) || !text.includes(`@${person.displayName}`)) return false;
    seen.add(person.id);
    return true;
  });
}

/** A comment's words cut into plain text and the names it mentions, so a mention can be shown in bold without any HTML from the text. */
export interface TextPart {
  readonly text: string;
  readonly mention: boolean;
}

export function splitMentions(text: string, mentions: readonly Colleague[]): TextPart[] {
  const names = mentions
    .map((person) => `@${person.displayName}`)
    .sort((a, b) => b.length - a.length);
  if (names.length === 0) return [{ text, mention: false }];
  const parts: TextPart[] = [];
  let rest = text;
  while (rest.length > 0) {
    let found = -1;
    let name = '';
    for (const candidate of names) {
      const index = rest.indexOf(candidate);
      if (index >= 0 && (found < 0 || index < found)) {
        found = index;
        name = candidate;
      }
    }
    if (found < 0) {
      parts.push({ text: rest, mention: false });
      break;
    }
    if (found > 0) parts.push({ text: rest.slice(0, found), mention: false });
    parts.push({ text: name, mention: true });
    rest = rest.slice(found + name.length);
  }
  return parts;
}
