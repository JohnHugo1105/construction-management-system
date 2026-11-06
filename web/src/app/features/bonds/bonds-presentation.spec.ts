import {
  bondActions,
  bondLook,
  bondTitle,
  directionWords,
  expiredAgo,
  expiresIn,
  historyLine,
  needsAttention,
  stateFilter,
} from './bonds-presentation';
import { event, listItem } from './bonds.fixtures';

describe('bond presentation', () => {
  it('names a bond by its kind and number', () => {
    expect(bondTitle(listItem())).toBe('Performance bond PB-2026-014');
    expect(bondTitle(listItem({ kind: 'AdvancePayment', bondNumber: 'AP-7' }))).toBe(
      'Advance payment bond AP-7',
    );
    expect(bondTitle(listItem({ kind: 'Other', bondNumber: 'X-1' }))).toBe('Bond X-1');
  });

  it('says who gave the bond to whom', () => {
    expect(directionWords(listItem())).toBe('Posted to the client');
    expect(
      directionWords(listItem({ direction: 'Received', otherPartyName: 'Dela Cruz Electrical' })),
    ).toBe('Received from Dela Cruz Electrical');
    expect(directionWords(listItem({ direction: 'Received' }))).toBe('Received from a firm');
  });

  it('words the days left in plain language', () => {
    expect(expiresIn(12)).toBe('Expires in 12 days');
    expect(expiresIn(1)).toBe('Expires tomorrow');
    expect(expiresIn(0)).toBe('Expires today');
    expect(expiredAgo(-3)).toBe('Expired 3 days ago');
    expect(expiredAgo(-1)).toBe('Expired yesterday');
  });

  it('chooses the chip from what the server says, never from the date', () => {
    expect(bondLook(listItem())).toMatchObject({ label: 'Valid', tone: 'success' });
    expect(bondLook(listItem({ expiry: 'ExpiringSoon', daysLeft: 30 }))).toMatchObject({
      label: 'Expires in 30 days',
      tone: 'warning',
    });
    expect(bondLook(listItem({ expiry: 'ExpiringSoon', daysLeft: 0 }))).toMatchObject({
      label: 'Expires today',
    });
    expect(bondLook(listItem({ expiry: 'Expired', daysLeft: -1 }))).toMatchObject({
      label: 'Expired',
      tone: 'danger',
    });
    expect(bondLook(listItem({ status: 'Released', expiry: null, daysLeft: null }))).toMatchObject({
      label: 'Released',
      tone: 'neutral',
    });
    expect(bondLook(listItem({ status: 'Called', expiry: null, daysLeft: null }))).toMatchObject({
      label: 'Called',
      tone: 'warning',
    });
    expect(bondLook(listItem({ status: 'Void', expiry: null, daysLeft: null }))).toMatchObject({
      label: 'Voided',
    });
  });

  it('flags only an Active bond that has lapsed or is about to', () => {
    expect(needsAttention(listItem())).toBe(false);
    expect(needsAttention(listItem({ expiry: 'ExpiringSoon', daysLeft: 5 }))).toBe(true);
    expect(needsAttention(listItem({ expiry: 'Expired', daysLeft: -2 }))).toBe(true);
    expect(needsAttention(listItem({ status: 'Released', expiry: null }))).toBe(false);
  });

  it('turns the register’s state into the API filters, with expiry implying Active', () => {
    expect(stateFilter('Active')).toEqual({ status: 'Active', expiry: '' });
    expect(stateFilter('Expired')).toEqual({ status: '', expiry: 'Expired' });
    expect(stateFilter('ExpiringSoon')).toEqual({ status: '', expiry: 'ExpiringSoon' });
    expect(stateFilter('Released')).toEqual({ status: 'Released', expiry: '' });
    expect(stateFilter('Called')).toEqual({ status: 'Called', expiry: '' });
    expect(stateFilter('Void')).toEqual({ status: 'Void', expiry: '' });
    expect(stateFilter('All')).toEqual({ status: '', expiry: '' });
  });

  it('offers steps only to a manager, on an Active bond, on a project that is not archived', () => {
    const active = { status: 'Active', projectArchived: false } as const;
    expect(bondActions(active, true)).toEqual({
      edit: true,
      extend: true,
      release: true,
      markCalled: true,
      voidBond: true,
    });
    expect(bondActions(active, false)).toEqual({
      edit: false,
      extend: false,
      release: false,
      markCalled: false,
      voidBond: false,
    });
    expect(bondActions({ ...active, projectArchived: true }, true)).toEqual({
      edit: false,
      extend: false,
      release: false,
      markCalled: false,
      voidBond: false,
    });
  });

  it('leaves a closed bond only the void step, and a voided one nothing', () => {
    const only = { edit: false, extend: false, release: false, markCalled: false, voidBond: true };
    expect(bondActions({ status: 'Released', projectArchived: false }, true)).toEqual(only);
    expect(bondActions({ status: 'Called', projectArchived: false }, true)).toEqual(only);
    expect(bondActions({ status: 'Void', projectArchived: false }, true)).toEqual({
      ...only,
      voidBond: false,
    });
  });

  it('writes each step of the history in words with who and when in the company time zone', () => {
    const zone = 'Asia/Manila';
    expect(historyLine(event(), zone)).toEqual({
      id: 'e1',
      title: 'Recorded',
      meta: 'Alma Accountant, 2 Nov 2026, 10:15 am',
      note: null,
    });
    expect(
      historyLine(
        event({
          kind: 'Extended',
          previousValidUntil: '2027-02-10',
          newValidUntil: '2027-05-10',
          note: 'Renewed for 90 days',
        }),
        zone,
      ),
    ).toMatchObject({
      title: 'Extended from 10 Feb 2027 to 10 May 2027',
      note: 'Renewed for 90 days',
    });
    expect(
      historyLine(
        event({ kind: 'Edited', previousValidUntil: '2027-02-10', newValidUntil: '2027-03-01' }),
        zone,
      ).title,
    ).toBe('Details changed, valid-until date moved from 10 Feb 2027 to 1 Mar 2027');
    expect(historyLine(event({ kind: 'Edited' }), zone).title).toBe('Details changed');
    expect(historyLine(event({ kind: 'Released', eventDate: '2026-12-03' }), zone).title).toBe(
      'Released on 3 Dec 2026',
    );
    expect(
      historyLine(event({ kind: 'Called', eventDate: '2026-12-03', byName: null }), zone),
    ).toMatchObject({
      title: 'Marked as called on 3 Dec 2026',
      meta: 'Someone, 2 Nov 2026, 10:15 am',
    });
    expect(historyLine(event({ kind: 'Voided', note: 'Entered twice' }), zone)).toMatchObject({
      title: 'Voided',
      note: 'Entered twice',
    });
  });
});
