import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { SessionState } from '../../core/session/session-state';
import { settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ruleViolation, validationError } from '../workforce/workforce.fixtures';
import { DiaryFormDialog } from './diary-form-dialog';
import { SiteDiaryApi } from './site-diary-api';
import { TODAY, WRITER, daysFromToday, entry } from './site-diary.fixtures';
import { DiaryEntry, DiaryFilledIn } from './site-diary.models';

type Call = (...args: unknown[]) => Observable<unknown>;

const quietDay: DiaryFilledIn = { onSite: { count: 0, names: [] }, deliveries: [] };
const busyDay: DiaryFilledIn = {
  onSite: { count: 2, names: ['Ana Abad', 'Juan Reyes'] },
  deliveries: [{ supplierName: 'Visayas Steel Corp.', description: 'Rebar 16 mm', unit: 'pc', quantity: 55 }],
};

async function open(options: { entry?: DiaryEntry; initialDate?: string; filledIn?: DiaryFilledIn; api?: Record<string, Call> } = {}) {
  const filledIn = vi.fn((projectId: string, date: string) => {
    void projectId;
    void date;
    return of(options.filledIn ?? busyDay);
  });
  const saved = vi.fn();
  const reloaded = vi.fn();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: SiteDiaryApi, useValue: { filledIn, ...options.api } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: WRITER,
  });
  const fixture = TestBed.createComponent(DiaryFormDialog);
  fixture.componentRef.setInput('projectId', 'p1');
  if (options.entry) fixture.componentRef.setInput('entry', options.entry);
  if (options.initialDate) fixture.componentRef.setInput('initialDate', options.initialDate);
  fixture.componentInstance.saved.subscribe(saved);
  fixture.componentInstance.reloaded.subscribe(reloaded);
  await settle(fixture);
  return { fixture, saved, reloaded, filledIn };
}

const submit = () => document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
const chip = (label: string) =>
  Array.from(document.body.querySelectorAll<HTMLButtonElement>('button.filter-chip')).find(
    (button) => button.textContent?.trim() === label,
  )!;

describe('DiaryFormDialog', () => {
  it('opens on today with the weather chips, the work done and the day’s attendance and deliveries read for it', async () => {
    const { filledIn } = await open();

    expect(texts(document.body, 'button.filter-chip')).toEqual(['Fair', 'Cloudy', 'Rain', 'Heavy rain', 'Storm']);
    expect(chip('Fair').getAttribute('aria-pressed')).toBe('true');
    expect(document.body.querySelector<HTMLInputElement>('input[formcontrolname="reportDate"]')!.value).toBe(TODAY);
    expect(document.body.querySelector<HTMLInputElement>('input[formcontrolname="reportDate"]')!.max).toBe(TODAY);
    expect(filledIn).toHaveBeenCalledWith('p1', TODAY);
    expect(text(document.body, 'app-diary-filled-in')).toContain('On site today');
    expect(text(document.body, 'app-diary-filled-in')).toContain('2 workers with attendance recorded.');
    expect(text(document.body, 'app-diary-filled-in')).toContain('Ana Abad, Juan Reyes');
    expect(text(document.body, 'app-diary-filled-in')).toContain('Delivered today');
    expect(text(document.body, 'app-diary-filled-in')).toContain('Rebar 16 mm');
    expect(text(document.body, 'app-diary-filled-in')).toContain('55 pc');
    expect(text(document.body, 'app-diary-filled-in')).toContain('Visayas Steel Corp.');
    // The panels have no field to type in, and the less usual notes are one step away.
    expect(document.body.querySelector('app-diary-filled-in input, app-diary-filled-in textarea')).toBeNull();
    expect(document.body.querySelector('details')!.hasAttribute('open')).toBe(false);
  });

  it('shows no names when the server sent only a count, and says so plainly for a quiet day', async () => {
    await open({ filledIn: { onSite: { count: 3 }, deliveries: [] } });
    expect(text(document.body, 'app-diary-filled-in')).toContain('3 workers with attendance recorded.');
    expect(document.body.querySelector('app-diary-filled-in .text-small.text-muted')?.textContent).not.toContain('Abad');
  });

  it('says there is nothing yet for a quiet day', async () => {
    await open({ filledIn: quietDay, initialDate: daysFromToday(-1) });
    const panels = text(document.body, 'app-diary-filled-in');

    expect(panels).toContain('On site that day');
    expect(panels).toContain('No attendance has been recorded for that day yet.');
    expect(panels).toContain('No deliveries were received that day.');
  });

  it('reads the panels again when another day is chosen, and not for a day that has not come', async () => {
    const { fixture, filledIn } = await open();
    filledIn.mockClear();

    type(document.body, 'input[formcontrolname="reportDate"]', daysFromToday(-3));
    await settle(fixture);
    expect(filledIn).toHaveBeenCalledWith('p1', daysFromToday(-3));

    filledIn.mockClear();
    type(document.body, 'input[formcontrolname="reportDate"]', daysFromToday(2));
    await settle(fixture);
    expect(filledIn).not.toHaveBeenCalled();
    expect(document.body.textContent).toContain('A report can only be for today or an earlier day.');
  });

  it('asks for the work done and sends nothing until the form is complete', async () => {
    const create = vi.fn(() => of(entry()));
    const { fixture } = await open({ api: { create } });

    submit();
    await settle(fixture);

    expect(create).not.toHaveBeenCalled();
    expect(document.body.textContent).toContain('Say what work was done. On a day with no work, say why.');
  });

  it('refuses a worker count outside 0 to 2,000 in words and an empty one too', async () => {
    const create = vi.fn(() => of(entry()));
    const { fixture } = await open({ api: { create } });
    type(document.body, 'textarea[formcontrolname="workDone"]', 'Poured the slab.');

    type(document.body, 'input[formcontrolname="subcontractorWorkers"]', '2001');
    submit();
    await settle(fixture);
    expect(document.body.textContent).toContain('Enter how many subcontractor workers were on site, from 0 to 2,000.');

    type(document.body, 'input[formcontrolname="subcontractorWorkers"]', '');
    submit();
    await settle(fixture);
    expect(document.body.textContent).toContain('Enter how many subcontractor workers were on site, or 0.');
    expect(create).not.toHaveBeenCalled();
  });

  it('sends the report trimmed, with the chosen weather, and one request id', async () => {
    const create = vi.fn(() => of(entry()));
    const { fixture, saved } = await open({ api: { create } });

    chip('Heavy rain').click();
    await settle(fixture);
    expect(chip('Heavy rain').getAttribute('aria-pressed')).toBe('true');
    expect(chip('Fair').getAttribute('aria-pressed')).toBe('false');
    type(document.body, 'textarea[formcontrolname="workDone"]', '  Poured the slab.  ');
    type(document.body, 'input[formcontrolname="subcontractorWorkers"]', '8');
    type(document.body, 'textarea[formcontrolname="issuesAndDelays"]', '  Truck came late  ');
    submit();
    await settle(fixture);

    expect(create).toHaveBeenCalledWith(
      'p1',
      TODAY,
      {
        weather: 'HeavyRain',
        weatherNote: null,
        workDone: 'Poured the slab.',
        issuesAndDelays: 'Truck came late',
        visitorsAndInspections: null,
        safetyNotes: null,
        subcontractorWorkers: 8,
      },
      expect.any(String),
    );
    expect(saved).toHaveBeenCalledOnce();
  });

  it('keeps what was typed and shows the server’s words when the day already has a report', async () => {
    const create = vi
      .fn()
      .mockReturnValueOnce(
        throwError(() =>
          ruleViolation('There is already a site report for 2 Nov 2026 on this project. Open it to correct it, or void it to write it again.'),
        ),
      )
      .mockReturnValueOnce(of(entry()));
    const { fixture, saved, reloaded } = await open({ api: { create } });
    type(document.body, 'textarea[formcontrolname="workDone"]', 'Poured the slab.');

    submit();
    await settle(fixture);

    expect(document.body.textContent).toContain('There is already a site report for 2 Nov 2026 on this project.');
    expect(document.body.querySelector<HTMLTextAreaElement>('textarea[formcontrolname="workDone"]')!.value).toBe('Poured the slab.');
    expect(saved).not.toHaveBeenCalled();
    const firstId = (create.mock.calls[0] as unknown[])[3];

    // Trying again is the same request; the person can also reload the list.
    submit();
    await settle(fixture);
    expect((create.mock.calls[1] as unknown[])[3]).toBe(firstId);
    expect(saved).toHaveBeenCalledOnce();
    expect(reloaded).not.toHaveBeenCalled();
  });

  it('puts a refused field’s message under it and opens the notes when the problem is hidden there', async () => {
    const create = vi.fn(() => throwError(() => validationError({ safetyNotes: ['Safety notes can be at most 1000 characters.'] })));
    const { fixture } = await open({ api: { create } });
    type(document.body, 'textarea[formcontrolname="workDone"]', 'Poured the slab.');

    submit();
    await settle(fixture);

    expect(document.body.textContent).toContain('Safety notes can be at most 1000 characters.');
    expect(document.body.querySelector('details')!.hasAttribute('open')).toBe(true);
  });

  it('corrects an existing report: the day is fixed, the fields are filled, the notes are open, and the version goes with the save', async () => {
    const correct = vi.fn(() => of(entry({ version: 1 })));
    const report = entry({ version: 4 });
    const { fixture, saved, filledIn } = await open({ entry: report, api: { correct } });

    expect(document.body.querySelector('input[formcontrolname="reportDate"]')).toBeNull();
    expect(document.body.textContent).toContain('Report for');
    expect(document.body.querySelector<HTMLTextAreaElement>('textarea[formcontrolname="workDone"]')!.value).toBe(report.workDone);
    expect(chip('Rain').getAttribute('aria-pressed')).toBe('true');
    expect(document.body.querySelector('details')!.hasAttribute('open')).toBe(true);
    // The panels come with the report: nothing more is read.
    expect(filledIn).not.toHaveBeenCalled();
    expect(text(document.body, 'app-diary-filled-in')).toContain('Ana Abad, Juan Reyes');

    type(document.body, 'textarea[formcontrolname="workDone"]', 'Poured the slab and cured it.');
    submit();
    await settle(fixture);

    expect(correct).toHaveBeenCalledWith(
      'p1',
      report.reportDate,
      expect.objectContaining({ workDone: 'Poured the slab and cured it.', weather: 'Rain', weatherNote: 'Showers from 1 pm' }),
      4,
    );
    expect(saved).toHaveBeenCalledOnce();
  });
});
