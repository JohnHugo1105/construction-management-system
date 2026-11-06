/**
 * The project documents API as the server sends it (see docs/api/documents.md). Dates are business dates ("2026-12-03"); instants are
 * UTC. The server decides the expiry state and the days left, cleans file names and checks every file; nothing here works them out.
 */

export type DocumentCategory =
  | 'Contract'
  | 'Drawing'
  | 'Permit'
  | 'Insurance'
  | 'Certificate'
  | 'Photo'
  | 'Report'
  | 'Other';

export type DocumentStatus = 'Active' | 'Void';

/** How close an Active document with a valid-until date is to it, judged on the company's calendar day. */
export type DocumentExpiry = 'Valid' | 'ExpiringSoon' | 'Expired';

export type DocumentVersionKind = 'File' | 'Link';

/** Whether file storage can take an upload now (Available, NeedsSetup, Unavailable or Failing). A link always works. */
export type CapabilityStateName = 'Available' | 'NeedsSetup' | 'Unavailable' | 'Failing';

/** One version: a stored file (its cleaned name, type and size) or an https link. Where a file is stored is never sent. */
export interface DocumentVersion {
  readonly number: number;
  readonly kind: DocumentVersionKind;
  readonly fileName: string | null;
  readonly contentType: string | null;
  readonly sizeBytes: number | null;
  readonly sha256: string | null;
  readonly url: string | null;
  readonly note: string | null;
  readonly addedByName: string | null;
  readonly addedAtUtc: string;
}

/** One row of a project's list. `expiry` and `daysLeft` come only for an Active document with a valid-until date. */
export interface DocumentListItem {
  readonly id: string;
  readonly projectId: string;
  readonly title: string;
  readonly description: string | null;
  readonly category: DocumentCategory;
  readonly validUntil: string | null;
  readonly expiry: DocumentExpiry | null;
  readonly daysLeft: number | null;
  readonly status: DocumentStatus;
  /** The newest version. */
  readonly current: DocumentVersion;
  readonly version: number;
}

/** Whether a file can be uploaded now; when it can't, `message` says why in plain words and what to do instead. */
export interface DocumentUploads {
  readonly available: boolean;
  readonly state: CapabilityStateName;
  readonly message: string;
  readonly maximumBytes: number;
  /** Lower-case, without the dot ("pdf"). */
  readonly allowedExtensions: readonly string[];
}

/** A project's documents (Active first, by category then title; the voided ones last), one page at a time. */
export interface DocumentList {
  readonly items: readonly DocumentListItem[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly uploads: DocumentUploads;
}

/** One document with its versions, newest first. */
export interface ProjectDocument {
  readonly id: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly projectArchived: boolean;
  readonly title: string;
  readonly description: string | null;
  readonly category: DocumentCategory;
  readonly validUntil: string | null;
  readonly expiry: DocumentExpiry | null;
  readonly daysLeft: number | null;
  readonly status: DocumentStatus;
  readonly voidReason: string | null;
  readonly voidedByName: string | null;
  readonly voidedAtUtc: string | null;
  readonly createdByName: string | null;
  readonly createdAtUtc: string;
  readonly currentVersionNumber: number;
  readonly versions: readonly DocumentVersion[];
  readonly version: number;
}

/** What a person types about a document. `validUntil` is "2026-12-03" or null (only for a permit, a policy or a certificate). */
export interface DocumentDetailsInput {
  readonly title: string;
  readonly description: string | null;
  readonly category: DocumentCategory;
  readonly validUntil: string | null;
}

/** A new document whose first version is an https link. */
export interface NewDocumentLinkInput extends DocumentDetailsInput {
  readonly url: string;
  readonly note: string | null;
}

/** A new document whose first version is a file. */
export interface NewDocumentFileInput extends DocumentDetailsInput {
  readonly file: File;
  readonly note: string | null;
}

/** A newer version: a file or a link, an optional note, and a renewed valid-until date (null keeps the date). */
export interface NewVersionInput {
  readonly file: File | null;
  readonly url: string | null;
  readonly note: string | null;
  readonly validUntil: string | null;
}
