import { describeRemaining, describeUsed, barWidthPercent, describeBasis } from './figures';
import {
  HEALTH_PRESENTATION,
  countAttention,
  describeAttention,
  healthOf,
  presentHealth,
  sortBySeverity,
} from './health';

describe('health presentation', () => {
  it('words every health with an icon as well as colour', () => {
    expect(Object.values(HEALTH_PRESENTATION).map((item) => item.label)).toEqual([
      'Over budget',
      'Unbudgeted',
      'Near limit',
      'On track',
      'No activity',
    ]);
    for (const item of Object.values(HEALTH_PRESENTATION)) {
      expect(item.icon).toBeTruthy();
      expect(item.meaning.length).toBeGreaterThan(10);
    }
  });

  it('uses the tones the budget chips already use', () => {
    expect(presentHealth('OverBudget').tone).toBe('danger');
    expect(presentHealth('NearLimit').tone).toBe('warning');
    expect(presentHealth('OnTrack').tone).toBe('success');
    expect(presentHealth('Unbudgeted').tone).toBe('unbudgeted');
    expect(presentHealth('NoActivity').tone).toBe('neutral');
  });

  it('treats a word it does not know as no activity instead of failing', () => {
    expect(healthOf('SomethingNew')).toBe('NoActivity');
    expect(healthOf(null)).toBe('NoActivity');
    expect(healthOf('toString')).toBe('NoActivity');
  });
});

describe('sortBySeverity', () => {
  const rows = [
    { name: 'Zeta', health: 'NoActivity' },
    { name: 'Beta', health: 'OnTrack' },
    { name: 'alpha', health: 'OnTrack' },
    { name: 'Gamma', health: 'NearLimit' },
    { name: 'Delta', health: 'Unbudgeted' },
    { name: 'Eta', health: 'OverBudget' },
    { name: 'Able', health: 'OverBudget' },
  ];

  it('puts over budget first, then unbudgeted, near limit, on track and no activity, each by name', () => {
    expect(sortBySeverity(rows).map((row) => row.name)).toEqual([
      'Able',
      'Eta',
      'Delta',
      'Gamma',
      'alpha',
      'Beta',
      'Zeta',
    ]);
  });

  it('does not change the list it was given', () => {
    const before = rows.map((row) => row.name);
    sortBySeverity(rows);
    expect(rows.map((row) => row.name)).toEqual(before);
  });
});

describe('attention counts', () => {
  it('counts only the states that need a look', () => {
    const counts = countAttention([
      { health: 'OverBudget' },
      { health: 'OverBudget' },
      { health: 'NearLimit' },
      { health: 'Unbudgeted' },
      { health: 'OnTrack' },
      { health: 'NoActivity' },
    ]);
    expect(counts).toEqual({ overBudget: 2, unbudgeted: 1, nearLimit: 1, total: 4 });
  });

  it('describes only the parts that are present, worst first', () => {
    expect(describeAttention({ overBudget: 1, unbudgeted: 0, nearLimit: 2, total: 3 })).toBe(
      '1 over budget, 2 near the limit',
    );
    expect(describeAttention({ overBudget: 0, unbudgeted: 1, nearLimit: 0, total: 1 })).toBe(
      '1 unbudgeted',
    );
    expect(describeAttention({ overBudget: 0, unbudgeted: 0, nearLimit: 0, total: 0 })).toBe('');
  });
});

describe('figures', () => {
  it('shows a negative remaining as how far over the budget it is', () => {
    expect(describeRemaining(1070000000, 'PHP')).toEqual({ text: '₱10,700,000.00', over: false });
    expect(describeRemaining(-18075000, 'PHP')).toEqual({
      text: 'Over by ₱180,750.00',
      over: true,
    });
    expect(describeRemaining(0, 'PHP')).toEqual({ text: '₱0.00', over: false });
  });

  it('writes the share used, or a dash when nothing is budgeted', () => {
    expect(describeUsed(4026)).toBe('40.26%');
    expect(describeUsed(10519)).toBe('105.19%');
    expect(describeUsed(null)).toBe('–');
  });

  it('never draws more than a full bar', () => {
    expect(barWidthPercent(4026)).toBeCloseTo(40.26);
    expect(barWidthPercent(10519)).toBe(100);
    expect(barWidthPercent(null)).toBe(0);
    expect(barWidthPercent(0)).toBe(0);
  });

  it('states the VAT basis in one plain line', () => {
    expect(describeBasis('IncludingVat')).toBe('Amounts include VAT');
    expect(describeBasis('ExcludingVat')).toBe('Amounts exclude VAT');
  });
});
