import { rowLink, urgentChip } from '../needs-action/needs-action-presentation';
import { documentItem, fileVersion, linkVersion, permit } from './documents.fixtures';
import {
  categoryChoices,
  documentActions,
  documentChip,
  fileProblem,
  groupDocuments,
  sizeWords,
  versionLine,
} from './documents-presentation';

const pdf = (size: number, name = 'plan.pdf') => new File([new Uint8Array(size)], name);

describe('documentChip', () => {
  it('says how close a dated document is, in words, never colour alone', () => {
    expect(documentChip(permit({ daysLeft: 12 }))).toEqual({
      label: 'Expires in 12 days',
      tone: 'warning',
      icon: 'clock',
    });
    expect(documentChip(permit({ daysLeft: 1 }))?.label).toBe('Expires tomorrow');
    expect(documentChip(permit({ daysLeft: 0 }))?.label).toBe('Expires today');
    expect(documentChip(permit({ expiry: 'Expired', daysLeft: -3 }))).toEqual({
      label: 'Expired',
      tone: 'danger',
      icon: 'error',
    });
    expect(
      documentChip(permit({ expiry: 'Valid', daysLeft: 200, validUntil: '2027-06-30' }))?.label,
    ).toBe('Valid until 30 Jun 2027');
  });

  it('shows Voided for a voided document and nothing for an undated active one', () => {
    expect(documentChip(permit({ status: 'Void', expiry: null, daysLeft: null }))?.label).toBe(
      'Voided',
    );
    expect(documentChip(documentItem())).toBeNull();
  });
});

describe('words for files and versions', () => {
  it('puts sizes in bytes, KB and MB', () => {
    expect(sizeWords(800)).toBe('800 bytes');
    expect(sizeWords(350 * 1024)).toBe('350 KB');
    expect(sizeWords(1_258_291)).toBe('1.2 MB');
    expect(sizeWords(25 * 1024 * 1024)).toBe('25 MB');
  });

  it('describes a version in one line in the company time zone', () => {
    expect(versionLine(fileVersion({ number: 2 }), 'Asia/Manila')).toBe(
      'Version 2 · PDF, 1.2 MB · Ana Reyes, 3 Dec 2026',
    );
    expect(
      versionLine(linkVersion({ addedAtUtc: '2026-12-03T17:00:00Z', addedByName: null }), 'Asia/Manila'),
    ).toBe('Version 1 · Link · Someone, 4 Dec 2026');
  });

  it('warns early about a type that is not accepted, an empty file and a file over the limit', () => {
    const allowed = ['pdf', 'png'];
    expect(fileProblem(pdf(10, 'page.html'), 100, allowed)).toBe(
      "This type of file can't be added. Use PDF or PNG.",
    );
    expect(fileProblem(pdf(0), 100, allowed)).toBe('The file is empty. Choose the file again.');
    expect(fileProblem(pdf(2 * 1024 * 1024 + 1), 2 * 1024 * 1024, allowed)).toBe(
      'The file is larger than 2 MB. Make it smaller, or add a link to where it is kept.',
    );
    expect(fileProblem(pdf(100, 'PLAN.PDF'), 100, allowed)).toBeNull();
  });
});

describe('who sees and does what', () => {
  it('offers contracts and insurance only to people who can see costs', () => {
    expect(categoryChoices(true).map((choice) => choice.value)).toContain('Contract');
    expect(categoryChoices(false).map((choice) => choice.value)).toEqual([
      'Drawing',
      'Permit',
      'Certificate',
      'Photo',
      'Report',
      'Other',
    ]);
  });

  it('offers changes only to a manager, on an active document of a project that is not archived', () => {
    const all = { addVersion: true, edit: true, voidDocument: true };
    const none = { addVersion: false, edit: false, voidDocument: false };
    expect(documentActions({ status: 'Active' }, true, false)).toEqual(all);
    expect(documentActions({ status: 'Active' }, false, false)).toEqual(none);
    expect(documentActions({ status: 'Active' }, true, true)).toEqual(none);
    expect(documentActions({ status: 'Void' }, true, false)).toEqual(none);
  });
});

describe('groupDocuments', () => {
  it('groups active documents by kind in the server order and puts every voided one last', () => {
    const groups = groupDocuments([
      documentItem({ id: 'a' }),
      permit({ id: 'b' }),
      documentItem({ id: 'c', category: 'Photo', title: 'Site photos' }),
      permit({ id: 'd', status: 'Void' }),
      documentItem({ id: 'e', status: 'Void' }),
    ]);

    expect(groups.map((group) => [group.heading, group.items.map((item) => item.id)])).toEqual([
      ['Drawings', ['a']],
      ['Permits', ['b']],
      ['Photos', ['c']],
      ['Voided', ['d', 'e']],
    ]);
  });
});

describe('Needs your action: documents expiring', () => {
  it('opens the project Documents tab and calls an expired document Expired', () => {
    const row = {
      id: 'd1',
      parentId: null,
      label: 'Building permit',
      projectId: 'p1',
      projectName: 'Riverside',
      detail: 'Expired 2 days ago',
      sinceUtc: null,
      dueDate: '2026-12-01',
      isUrgent: true,
    };
    expect(rowLink('documents-expiring', row, { can: () => true })).toEqual([
      '/projects',
      'p1',
      'documents',
    ]);
    expect(urgentChip('documents-expiring').label).toBe('Expired');
  });
});
