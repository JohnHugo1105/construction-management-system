import { Permission } from '../../core/session/permissions';
import {
  DocumentList,
  DocumentListItem,
  DocumentUploads,
  DocumentVersion,
  ProjectDocument,
} from './documents.models';

// Test data for the documents screens. Fictional.

export const PROJECT_ID = 'p-riverside';

/** A Project Manager: reads, manages and sees costs. */
export const MANAGER: readonly string[] = [
  Permission.projectsRead,
  Permission.costsRead,
  Permission.documentsRead,
  Permission.documentsManage,
];

/** A Warehouse Keeper: reads documents, never costs. */
export const READER: readonly string[] = [Permission.projectsRead, Permission.documentsRead];

export function fileVersion(overrides: Partial<DocumentVersion> = {}): DocumentVersion {
  return {
    number: 1,
    kind: 'File',
    fileName: 'Ground floor plan.pdf',
    contentType: 'application/pdf',
    sizeBytes: 1_258_291,
    sha256: 'a'.repeat(64),
    url: null,
    note: null,
    addedByName: 'Ana Reyes',
    addedAtUtc: '2026-12-03T02:00:00Z',
    ...overrides,
  };
}

export function linkVersion(overrides: Partial<DocumentVersion> = {}): DocumentVersion {
  return fileVersion({
    kind: 'Link',
    fileName: null,
    contentType: null,
    sizeBytes: null,
    sha256: null,
    url: 'https://files.example.com/permit.pdf',
    ...overrides,
  });
}

export function documentItem(overrides: Partial<DocumentListItem> = {}): DocumentListItem {
  return {
    id: 'd-plan',
    projectId: PROJECT_ID,
    title: 'Ground floor plan',
    description: null,
    category: 'Drawing',
    validUntil: null,
    expiry: null,
    daysLeft: null,
    status: 'Active',
    current: fileVersion(),
    version: 1,
    ...overrides,
  };
}

export const permit = (overrides: Partial<DocumentListItem> = {}): DocumentListItem =>
  documentItem({
    id: 'd-permit',
    title: 'Building permit',
    category: 'Permit',
    validUntil: '2026-12-15',
    expiry: 'ExpiringSoon',
    daysLeft: 12,
    current: linkVersion(),
    ...overrides,
  });

export const UPLOADS_OK: DocumentUploads = {
  available: true,
  state: 'Available',
  message: 'Files are kept on the server and offered only as downloads.',
  maximumBytes: 25 * 1024 * 1024,
  allowedExtensions: ['pdf', 'jpg', 'png', 'docx'],
};

export const UPLOADS_NOT_SET_UP: DocumentUploads = {
  ...UPLOADS_OK,
  available: false,
  state: 'NeedsSetup',
  message:
    "File storage is not set up yet, so files can't be uploaded. Your administrator sets the storage folder for the server. Add a link to the document instead.",
};

export function listOf(
  items: readonly DocumentListItem[],
  options: { total?: number; page?: number; uploads?: DocumentUploads } = {},
): DocumentList {
  return {
    items,
    total: options.total ?? items.length,
    page: options.page ?? 1,
    pageSize: 25,
    uploads: options.uploads ?? UPLOADS_OK,
  };
}

export function documentOf(item: DocumentListItem, versions?: readonly DocumentVersion[]): ProjectDocument {
  return {
    id: item.id,
    projectId: item.projectId,
    projectName: 'Riverside Residences',
    projectArchived: false,
    title: item.title,
    description: item.description,
    category: item.category,
    validUntil: item.validUntil,
    expiry: item.expiry,
    daysLeft: item.daysLeft,
    status: item.status,
    voidReason: item.status === 'Void' ? 'Uploaded to the wrong project' : null,
    voidedByName: item.status === 'Void' ? 'Ana Reyes' : null,
    voidedAtUtc: item.status === 'Void' ? '2026-12-04T02:00:00Z' : null,
    createdByName: 'Ana Reyes',
    createdAtUtc: '2026-11-01T02:00:00Z',
    currentVersionNumber: item.current.number,
    versions: versions ?? [item.current],
    version: item.version,
  };
}
