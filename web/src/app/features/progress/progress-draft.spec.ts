import {
  LineDraft,
  buildRequest,
  draftBps,
  fieldText,
  isChanged,
  isLowering,
  percentProblem,
  placeServerErrors,
  steppedText,
} from './progress-draft';
import { COLUMNS_ID, FOOTINGS_ID, lineFixture } from './testing/progress-fixtures';

const footings = lineFixture(); // 50%
const columns = lineFixture({ budgetLineId: COLUMNS_ID, description: 'Columns', percentBps: 2000 });
const draft = (text: string, note = ''): LineDraft => ({ text, note });

describe('draftBps', () => {
  it.each([
    ['60', 6000],
    ['12.5', 1250],
    ['12.5%', 1250],
    ['0', 0],
    ['100', 10000],
  ])('reads %s as %d basis points', (text, bps) => {
    expect(draftBps(draft(text))).toBe(bps);
  });

  it.each(['', 'abc', '101', '100.01', '-5', '1.234'])('refuses "%s"', (text) => {
    expect(draftBps(draft(text))).toBeNull();
  });
});

describe('isChanged', () => {
  it('is false without a draft and when the typed percent is the saved one', () => {
    expect(isChanged(footings, undefined)).toBe(false);
    expect(isChanged(footings, draft('50'))).toBe(false);
  });

  it('is true for a different percent, and for text that is not a percent yet', () => {
    expect(isChanged(footings, draft('55'))).toBe(true);
    expect(isChanged(footings, draft('abc'))).toBe(true);
    expect(isChanged(footings, draft(''))).toBe(true);
  });
});

describe('percentProblem and isLowering', () => {
  it('names a typed percent that is not one, and nothing for a good one', () => {
    expect(percentProblem(draft('abc'))).toBe('Enter a percent between 0 and 100.');
    expect(percentProblem(draft('55'))).toBeNull();
    expect(percentProblem(undefined)).toBeNull();
  });

  it('asks for a reason only when the figure goes down', () => {
    expect(isLowering(footings, draft('45'))).toBe(true);
    expect(isLowering(footings, draft('50'))).toBe(false);
    expect(isLowering(footings, draft('60'))).toBe(false);
    expect(isLowering(footings, draft('abc'))).toBe(false);
    expect(isLowering(footings, undefined)).toBe(false);
  });
});

describe('steppedText', () => {
  it('moves by 5 points from the saved percent or from what is typed', () => {
    expect(steppedText(footings, undefined, 1)).toBe('55');
    expect(steppedText(footings, undefined, -1)).toBe('45');
    expect(steppedText(footings, draft('57.5'), 1)).toBe('62.5');
  });

  it('stays between 0 and 100', () => {
    expect(steppedText(lineFixture({ percentBps: 9800 }), undefined, 1)).toBe('100');
    expect(steppedText(lineFixture({ percentBps: 200 }), undefined, -1)).toBe('0');
  });

  it('starts from the saved percent when the typed text is not a percent', () => {
    expect(steppedText(footings, draft('abc'), 1)).toBe('55');
  });
});

describe('fieldText', () => {
  it('shows what was typed, else the saved percent', () => {
    expect(fieldText(footings, undefined)).toBe('50');
    expect(fieldText(footings, draft('5.'))).toBe('5.');
    expect(fieldText(lineFixture({ percentBps: 1250 }), undefined)).toBe('12.5');
  });
});

describe('buildRequest', () => {
  const lines = [footings, columns];

  it('sends changed lines in display order, each with the percent the person saw', () => {
    const drafts = new Map([
      [COLUMNS_ID, draft('30', '  ')],
      [FOOTINGS_ID, draft('40', ' Two footings redone ')],
    ]);

    expect(buildRequest(lines, drafts, '2026-11-02', 'req-1')).toEqual({
      asOfDate: '2026-11-02',
      clientRequestId: 'req-1',
      lines: [
        { budgetLineId: FOOTINGS_ID, percentBps: 4000, note: 'Two footings redone', expectedPercentBps: 5000 },
        { budgetLineId: COLUMNS_ID, percentBps: 3000, note: null, expectedPercentBps: 2000 },
      ],
    });
  });

  it('leaves out lines that did not change or are not a percent, and is null when nothing is left', () => {
    const drafts = new Map([
      [FOOTINGS_ID, draft('50')],
      [COLUMNS_ID, draft('abc')],
    ]);

    expect(buildRequest(lines, drafts, '2026-11-02', 'req-1')).toBeNull();
    expect(buildRequest(lines, new Map(), '2026-11-02', 'req-1')).toBeNull();
  });
});

describe('placeServerErrors', () => {
  const order = [FOOTINGS_ID, COLUMNS_ID];

  it('puts line messages on the line at that position and the rest on the form', () => {
    const placed = placeServerErrors(
      {
        'lines[1].note': ['Say why the progress went down, for example rework.'],
        'lines[0].percentBps': ['Enter a percent between 0 and 100.'],
        'lines[0].budgetLineId': ['That budget line isn’t part of this project any more.'],
        asOfDate: ['Progress can’t be dated in the future.'],
        lines: ['Update at most 200 lines at once.'],
      },
      order,
    );

    expect(placed.lines.get(COLUMNS_ID)).toEqual({ note: 'Say why the progress went down, for example rework.' });
    expect(placed.lines.get(FOOTINGS_ID)).toEqual({
      percent: 'Enter a percent between 0 and 100.',
      line: 'That budget line isn’t part of this project any more.',
    });
    expect(placed.form).toEqual(['Progress can’t be dated in the future.', 'Update at most 200 lines at once.']);
  });

  it('keeps a message for a position that does not exist on the form', () => {
    const placed = placeServerErrors({ 'lines[7].note': ['Odd.'] }, order);

    expect(placed.lines.size).toBe(0);
    expect(placed.form).toEqual(['Odd.']);
  });
});
