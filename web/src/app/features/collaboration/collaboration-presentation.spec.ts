import {
  insertMention,
  mentionQueryAt,
  mentionsStillIn,
  recordLink,
  splitMentions,
  taskLook,
  unreadBadge,
} from './collaboration-presentation';

const pia = { id: 'u2', displayName: 'Pia Santos' };
const ben = { id: 'u3', displayName: 'Ben Cruz' };

describe('collaboration presentation', () => {
  it('words a task state with an icon, and an Open task past its date as Overdue', () => {
    expect(taskLook({ status: 'Open', isOverdue: false }).label).toBe('Open');
    expect(taskLook({ status: 'Open', isOverdue: true })).toEqual({
      label: 'Overdue',
      tone: 'danger',
      icon: 'clock',
    });
    expect(taskLook({ status: 'Done', isOverdue: false }).label).toBe('Done');
    expect(taskLook({ status: 'Cancelled', isOverdue: false }).label).toBe('Cancelled');
  });

  it('opens each record on its own page, a change order on its project Changes tab', () => {
    expect(recordLink('Task', 't1', null).link).toEqual(['/tasks', 't1']);
    expect(recordLink('PurchaseOrder', 'po1', 'p1').link).toEqual(['/purchasing/orders', 'po1']);
    expect(recordLink('ClientBill', 'b1', 'p1').link).toEqual(['/billing/bills', 'b1']);
    expect(recordLink('Subcontract', 's1', 'p1').link).toEqual(['/subcontracts', 's1']);
    expect(recordLink('Project', 'p1', 'p1').link).toEqual(['/projects', 'p1']);
    expect(recordLink('ChangeOrder', 'co1', 'p1')).toEqual({
      link: ['/projects', 'p1', 'changes'],
      queryParams: { order: 'co1' },
    });
  });

  it('shows nothing at 0 unread and 99+ above 99', () => {
    expect(unreadBadge(0)).toBe('');
    expect(unreadBadge(7)).toBe('7');
    expect(unreadBadge(99)).toBe('99');
    expect(unreadBadge(132)).toBe('99+');
  });

  it('finds the name being typed after an @, but not an email address', () => {
    expect(mentionQueryAt('Ask @pi', 7)).toEqual({ start: 4, search: 'pi' });
    expect(mentionQueryAt('@', 1)).toEqual({ start: 0, search: '' });
    expect(mentionQueryAt('Ask @Pia San', 12)).toEqual({ start: 4, search: 'Pia San' });
    expect(mentionQueryAt('mail ana@example.test', 21)).toBeNull();
    expect(mentionQueryAt('No mention here', 15)).toBeNull();
    expect(mentionQueryAt('Ask @pia\nnext line', 18)).toBeNull();
  });

  it('puts the chosen name in place of what was typed and moves the caret after it', () => {
    const text = 'Ask @pi about it';
    const result = insertMention(text, { start: 4, search: 'pi' }, 7, 'Pia Santos');
    expect(result).toEqual({ text: 'Ask @Pia Santos about it', caret: 16 });
    expect(insertMention('@b', { start: 0, search: 'b' }, 2, 'Ben Cruz')).toEqual({
      text: '@Ben Cruz ',
      caret: 10,
    });
  });

  it('mentions only the people whose @Name is still in the words, once each', () => {
    expect(mentionsStillIn('@Pia Santos please check', [pia, ben, pia])).toEqual([pia]);
    expect(mentionsStillIn('nobody', [pia])).toEqual([]);
  });

  it('cuts the words around the mentions so they can be shown in bold', () => {
    expect(splitMentions('Ask @Pia Santos and @Ben Cruz.', [pia, ben])).toEqual([
      { text: 'Ask ', mention: false },
      { text: '@Pia Santos', mention: true },
      { text: ' and ', mention: false },
      { text: '@Ben Cruz', mention: true },
      { text: '.', mention: false },
    ]);
    expect(splitMentions('<b>plain</b>', [])).toEqual([{ text: '<b>plain</b>', mention: false }]);
  });
});
