import {
  DraftRow,
  checkRows,
  dayTypeChip,
  draftRowsFor,
  holidayLine,
  hoursWords,
  isFutureDate,
  mapBatchErrors,
  planBatch,
  rowsToSave,
} from './attendance-sheet';
import { formatHours, parseHours } from './hours-field';
import { WorkerLookup } from './workforce.models';

const crew: WorkerLookup[] = [
  { id: 'w1', fullName: 'Ana Bautista', employeeNumber: 'W-005', status: 'Active' },
  { id: 'w2', fullName: 'Juan Dela Cruz', employeeNumber: 'W-001', status: 'Active' },
  { id: 'w3', fullName: 'Jose Garcia', employeeNumber: 'W-004', status: 'Active' },
];

describe('attendance sheet lines', () => {
  it('starts every crew member as a full company day with no overtime', () => {
    const rows = draftRowsFor(crew, 8);

    expect(
      rows.map((row) => [row.workerId, row.worked, row.regular, row.overtime, row.remarks]),
    ).toEqual([
      ['w1', true, 8, 0, ''],
      ['w2', true, 8, 0, ''],
      ['w3', true, 8, 0, ''],
    ]);
  });

  it('uses the company day, not a fixed eight hours', () => {
    expect(draftRowsFor(crew, 10)[0]?.regular).toBe(10);
  });

  it('leaves out of what is saved the workers who did not work', () => {
    const rows = draftRowsFor(crew, 8).map((row) =>
      row.workerId === 'w2' ? { ...row, worked: false } : row,
    );

    expect(rowsToSave(rows).map((row) => row.workerId)).toEqual(['w1', 'w3']);
  });
});

describe('checking lines before they are sent', () => {
  const rows = (patch: Partial<DraftRow>): DraftRow[] => [
    { ...draftRowsFor(crew, 8)[0]!, ...patch },
  ];

  it('accepts a full day', () => {
    expect(checkRows(rows({}), 8).size).toBe(0);
  });

  it('asks for blank hours instead of sending zero', () => {
    const problems = checkRows(rows({ regular: null }), 8).get('w1');
    expect(problems?.regular).toContain('Enter the regular hours');
  });

  it('stops regular hours above the company day and points at overtime', () => {
    const problems = checkRows(rows({ regular: 9 }), 8).get('w1');
    expect(problems?.regular).toBe(
      'Regular hours are at most 8 a day. Enter the rest as overtime.',
    );
  });

  it('asks for the hours worked when both boxes are zero', () => {
    expect(checkRows(rows({ regular: 0, overtime: 0 }), 8).get('w1')?.regular).toContain(
      'Enter the hours worked',
    );
  });

  it('does not check a worker who is not working', () => {
    expect(checkRows(rows({ worked: false, regular: null }), 8).size).toBe(0);
  });
});

describe('the batch sent to the server', () => {
  it('sends only workers who worked, with trimmed remarks, and remembers their order', () => {
    const rows = draftRowsFor(crew, 8).map((row) => {
      if (row.workerId === 'w1') return { ...row, worked: false };
      if (row.workerId === 'w2') return { ...row, overtime: 2, remarks: '  left early  ' };
      return row;
    });

    const plan = planBatch('p1', null, '2026-11-01', rows, 'request-1');

    expect(plan.request).toEqual({
      projectId: 'p1',
      subProjectId: null,
      workDate: '2026-11-01',
      clientRequestId: 'request-1',
      entries: [
        { workerId: 'w2', regularHours: 8, overtimeHours: 2, nightHours: 0, remarks: 'left early' },
        { workerId: 'w3', regularHours: 8, overtimeHours: 0, nightHours: 0, remarks: null },
      ],
    });
    expect(plan.order).toEqual(['w2', 'w3']);
  });
});

describe('mapping the API’s errors onto lines', () => {
  const order = ['w2', 'w3'];

  it('puts entries[i].field on the i-th worker that was sent, in the right box', () => {
    const mapped = mapBatchErrors(
      {
        'entries[0].overtimeHours': ['A day has 24 hours. Check the hours entered.'],
        'entries[1].regularHours': [
          'Regular hours are at most 8 a day. Enter the rest as overtime.',
        ],
        'entries[1].remarks': ['The remarks can be at most 300 characters.'],
      },
      order,
    );

    expect(mapped.rows.get('w2')).toEqual({
      overtime: 'A day has 24 hours. Check the hours entered.',
    });
    expect(mapped.rows.get('w3')).toEqual({
      regular: 'Regular hours are at most 8 a day. Enter the rest as overtime.',
      remarks: 'The remarks can be at most 300 characters.',
    });
    expect(mapped.general).toEqual([]);
  });

  it('puts a refusal about the worker herself on her whole line', () => {
    const mapped = mapBatchErrors(
      {
        'entries[1].workerId': ['Jose Garcia already has attendance on Riverside for 1 Nov 2026.'],
      },
      order,
    );
    expect(mapped.rows.get('w3')?.worker).toContain('already has attendance');
  });

  it('keeps messages that belong to the whole sheet apart from the lines', () => {
    const mapped = mapBatchErrors(
      {
        workDate: ["The work date can't be in the future."],
        entries: ['Add at least one worker.'],
        'entries[7].regularHours': ['A position that was never sent.'],
      },
      order,
    );

    expect(mapped.rows.size).toBe(0);
    expect(mapped.general).toEqual([
      "The work date can't be in the future.",
      'Add at least one worker.',
      'A position that was never sent.',
    ]);
  });
});

describe('hours text', () => {
  it('reads what people type, to two decimals, and nothing else', () => {
    expect(parseHours('8')).toBe(8);
    expect(parseHours('7.5')).toBe(7.5);
    expect(parseHours('0.25')).toBe(0.25);
    expect(parseHours('.5')).toBe(0.5);
    expect(parseHours('')).toBeNull();
    expect(parseHours('abc')).toBeNull();
    expect(parseHours('1.234')).toBeNull();
    expect(parseHours('-1')).toBeNull();
  });

  it('writes hours without needless zeros', () => {
    expect(formatHours(8)).toBe('8');
    expect(formatHours(7.5)).toBe('7.5');
    expect(formatHours(null)).toBe('');
  });

  it('words hours as recorded, never as pay', () => {
    expect(hoursWords(8, 0)).toBe('8 h');
    expect(hoursWords(8, 2.5)).toBe('8 h + 2.5 h overtime');
    expect(hoursWords(8, 2, 3)).toBe('8 h + 2 h overtime, 3 h at night');
    expect(hoursWords(8, 0, 0)).toBe('8 h');
  });

  it('shows a chip for a rest day or holiday as the server decided it, and none for an ordinary day', () => {
    expect(dayTypeChip('Ordinary')).toBeNull();
    expect(dayTypeChip(undefined)).toBeNull();
    expect(dayTypeChip('RestDay')?.label).toBe('Rest day');
    expect(dayTypeChip('RegularHolidayRestDay')).toEqual({
      label: 'Regular holiday, rest day',
      tone: 'warning',
      icon: 'calendar',
    });
  });

  it('names the company holiday on the sheet’s day in one line', () => {
    expect(holidayLine({ holidayName: null, holidayType: null })).toBeNull();
    expect(holidayLine({ holidayName: 'Founders day', holidayType: 'Regular' })).toBe(
      'Regular holiday: Founders day',
    );
    expect(holidayLine({ holidayName: 'Foundation', holidayType: 'Special' })).toBe(
      'Special day: Foundation',
    );
  });

  it('puts the server’s night-hours message on the row it is about', () => {
    const mapped = mapBatchErrors(
      { 'entries[0].nightHours': ['Night hours can’t be more than the hours worked that day.'] },
      ['w2'],
    );
    expect(mapped.rows.get('w2')).toEqual({
      night: 'Night hours can’t be more than the hours worked that day.',
    });
  });

  it('knows a day after today is in the future', () => {
    expect(isFutureDate('2026-11-02', '2026-11-01')).toBe(true);
    expect(isFutureDate('2026-11-01', '2026-11-01')).toBe(false);
    expect(isFutureDate('2026-10-31', '2026-11-01')).toBe(false);
  });
});
