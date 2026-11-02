import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { buttonNamed, flushMicrotasks, settle, text } from '../../testing/dom';
import { KioskApi, KioskSendResult } from './kiosk-api';
import { FEEDBACK_MS, KioskPage, RESEND_MS } from './kiosk-page';
import { KioskScan, PunchResult } from './timeclock.models';

type Send = (key: string, scans: readonly KioskScan[]) => Observable<KioskSendResult>;

const accepted = (overrides: Partial<PunchResult> = {}): PunchResult => ({
  clientPunchId: 'x',
  outcome: 'Accepted',
  displayName: 'Juan Dela Cruz',
  direction: 'In',
  punchedAt: '2026-11-01T23:02:00Z',
  localTime: '07:02',
  notPostedHere: false,
  reasonCode: null,
  reason: null,
  ...overrides,
});

const answered = (result: PunchResult): KioskSendResult => ({
  kind: 'answered',
  result: { results: [result] },
});

/** Answers each scan sent with its own id, the way the server does. */
const answerAll =
  (make: (scan: KioskScan) => PunchResult): Send =>
  (_key, scans) =>
    of({ kind: 'answered', result: { results: scans.map(make) } });

async function open(
  options: {
    key?: string | null;
    send?: Mock<Send>;
    checkKey?: Mock<(key: string) => Observable<boolean | null>>;
  } = {},
) {
  if (options.key !== null)
    localStorage.setItem('constructopx.kiosk.key', options.key ?? 'kiosk-key');
  const send = options.send ?? vi.fn<Send>(() => of(answered(accepted())));
  const checkKey = options.checkKey ?? vi.fn(() => of<boolean | null>(true));
  TestBed.configureTestingModule({
    providers: [{ provide: KioskApi, useValue: { send, checkKey } }],
  });
  const fixture = TestBed.createComponent(KioskPage);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, send, checkKey };
}

/** A keyboard-style scanner: it types the code one character at a time into the focused field, then presses Enter. */
async function scanWithScanner(fixture: { nativeElement: unknown }, code: string): Promise<void> {
  const root = fixture.nativeElement as HTMLElement;
  const input = root.querySelector<HTMLInputElement>('input[name="scan"]')!;
  for (const character of code) {
    input.dispatchEvent(new KeyboardEvent('keydown', { key: character, bubbles: true }));
    input.value += character;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }
  input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  await flushMicrotasks(6);
}

describe('KioskPage', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => vi.useRealTimers());

  it('asks for the device key on the first visit, warns to keep the tablet at the gate, and keeps an accepted key on the tablet', async () => {
    const { fixture, root, checkKey } = await open({ key: null });

    expect(text(root, '.alert__title')).toBe('Keep this tablet at the site gate');
    expect(root.querySelector('input[name="scan"]')).toBeNull();

    const input = root.querySelector<HTMLInputElement>('input[name="key"]')!;
    input.value = ' the-device-key ';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    buttonNamed(root, 'Start the kiosk').click();
    await settle(fixture);

    expect(checkKey).toHaveBeenCalledWith('the-device-key');
    expect(localStorage.getItem('constructopx.kiosk.key')).toBe('the-device-key');
    expect(text(root, '.kiosk__ready')).toBe('Scan your badge');
  });

  it('says plainly when a key is not accepted, and keeps nothing', async () => {
    const { fixture, root } = await open({
      key: null,
      checkKey: vi.fn(() => of<boolean | null>(false)),
    });

    const input = root.querySelector<HTMLInputElement>('input[name="key"]')!;
    input.value = 'wrong';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    buttonNamed(root, 'Start the kiosk').click();
    await settle(fixture);

    expect(text(root, '.field__error')).toBe(
      "That key wasn't accepted. Check it and try again, or ask your administrator for a new one.",
    );
    expect(localStorage.getItem('constructopx.kiosk.key')).toBeNull();
  });

  it('takes a scan from the scanner and answers with the name in large type and the direction in words', async () => {
    const { fixture, root, send } = await open();
    const scanner = root.querySelector<HTMLInputElement>('input[name="scan"]')!;
    expect(document.activeElement).toBe(scanner);

    await scanWithScanner(fixture, 'ABCD-EFGH-JKMN');
    await settle(fixture);

    expect(send).toHaveBeenCalledTimes(1);
    const [key, scans] = send.mock.calls[0]!;
    expect(key).toBe('kiosk-key');
    expect(scans).toHaveLength(1);
    expect(scans[0]!.workerCode).toBe('ABCD-EFGH-JKMN');
    expect(Number.isNaN(Date.parse(scans[0]!.punchedAt))).toBe(false);
    expect(text(root, '.kiosk__name')).toBe('Juan Dela Cruz');
    expect(text(root, '.kiosk__words')).toBe('Time in 7:02 am');
    expect(root.querySelector('.kiosk__answer--in svg')).not.toBeNull();
    // The field is cleared and ready for the next person.
    expect(scanner.value).toBe('');
  });

  it('says time out, already recorded, and not posted here in words', async () => {
    const results = [
      accepted({ direction: 'Out', localTime: '17:03', displayName: 'Ana Bautista' }),
      accepted({
        outcome: 'AlreadyRecorded',
        direction: 'Out',
        localTime: '17:03',
        displayName: 'Ana Bautista',
      }),
      accepted({ notPostedHere: true }),
    ];
    const send = vi.fn<Send>(() => of(answered(results.shift()!)));
    const { fixture, root } = await open({ send });

    await scanWithScanner(fixture, 'CODE1');
    await settle(fixture);
    expect([text(root, '.kiosk__name'), text(root, '.kiosk__words')]).toEqual([
      'Ana Bautista',
      'Time out 5:03 pm',
    ]);
    expect(root.querySelector('.kiosk__answer--out')).not.toBeNull();

    await scanWithScanner(fixture, 'CODE1');
    await settle(fixture);
    expect(text(root, '.kiosk__words')).toBe('Already recorded: time out 5:03 pm');
    expect(text(root, '.kiosk__note')).toBe("You don't need to scan again.");

    await scanWithScanner(fixture, 'CODE2');
    await settle(fixture);
    expect(text(root, '.kiosk__note')).toBe(
      "You're not on this site's list today. Your scan is kept; tell your supervisor.",
    );
  });

  it('gives each refusal in plain words and shows no one', async () => {
    const reasons = [
      "This badge isn't active. See your supervisor.",
      "The time on this machine is wrong, so the scan wasn't kept. Ask your supervisor to set the clock.",
    ];
    const queue = [...reasons];
    const send = vi.fn<Send>(() =>
      of(
        answered(
          accepted({
            outcome: 'Rejected',
            displayName: null,
            direction: null,
            localTime: null,
            reasonCode: 'BadgeNotActive',
            reason: queue.shift()!,
          }),
        ),
      ),
    );
    const { fixture, root } = await open({ send });

    for (const reason of reasons) {
      await scanWithScanner(fixture, 'ZZZZZZZZZZZZ');
      await settle(fixture);
      expect(text(root, '.kiosk__words')).toBe(reason);
      expect(root.querySelector('.kiosk__name')).toBeNull();
      expect(root.querySelector('.kiosk__answer--refused')).not.toBeNull();
    }
  });

  it('sends a code typed by hand when a badge will not scan', async () => {
    const { fixture, root, send } = await open();

    const typed = root.querySelector<HTMLInputElement>('input[name="code"]')!;
    typed.value = 'abcd efgh jkmn';
    buttonNamed(root, 'Send code').click();
    await flushMicrotasks(6);
    await settle(fixture);

    expect(send.mock.calls[0]![1][0]!.workerCode).toBe('abcd efgh jkmn');
    expect(typed.value).toBe('');
    expect(text(root, '.kiosk__name')).toBe('Juan Dela Cruz');
  });

  it('keeps a scan on the tablet with its real time when offline, shows how many wait, and sends them every 30 seconds', async () => {
    // Only the 30-second timer and the clock are faked: the page's rendering keeps its real timers.
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    vi.setSystemTime(new Date('2026-11-02T07:02:30'));
    let online = false;
    const send = vi.fn<Send>((key, scans) =>
      online
        ? answerAll((scan) => accepted({ clientPunchId: scan.clientPunchId }))(key, scans)
        : of({ kind: 'unreachable' }),
    );
    const { fixture, root } = await open({ send });

    await scanWithScanner(fixture, 'AAAABBBBCCCC');
    await scanWithScanner(fixture, 'DDDDEEEEFFFF');
    await settle(fixture);

    expect(text(root, '.kiosk__words')).toBe(
      'Saved on this tablet at 7:02 am. It will send itself when the connection is back.',
    );
    expect(text(root, '.kiosk__waiting')).toBe('2 scans waiting to send');
    const kept = JSON.parse(localStorage.getItem('constructopx.kiosk.queue')!) as KioskScan[];
    expect(kept.map((scan) => scan.workerCode)).toEqual(['AAAABBBBCCCC', 'DDDDEEEEFFFF']);
    expect(kept[0]!.punchedAt).toBe(new Date('2026-11-02T07:02:30').toISOString());

    // Still offline at the next attempt: nothing is lost.
    vi.advanceTimersByTime(RESEND_MS);
    await flushMicrotasks(6);
    await settle(fixture);
    expect(text(root, '.kiosk__waiting')).toBe('2 scans waiting to send');

    online = true;
    vi.advanceTimersByTime(RESEND_MS);
    await flushMicrotasks(6);
    await settle(fixture);
    const resent = send.mock.calls[send.mock.calls.length - 1]![1];
    expect(resent.map((scan) => scan.punchedAt)).toEqual(kept.map((scan) => scan.punchedAt));
    expect(root.querySelector('.kiosk__waiting')).toBeNull();
    expect(JSON.parse(localStorage.getItem('constructopx.kiosk.queue')!)).toEqual([]);
  });

  it('clears the answer after a few seconds, ready for the next person', async () => {
    const { fixture, root } = await open();
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });

    await scanWithScanner(fixture, 'AAAABBBBCCCC');
    fixture.detectChanges();
    expect(text(root, '.kiosk__name')).toBe('Juan Dela Cruz');

    vi.advanceTimersByTime(FEEDBACK_MS - 1);
    fixture.detectChanges();
    expect(text(root, '.kiosk__name')).toBe('Juan Dela Cruz');
    vi.advanceTimersByTime(1);
    fixture.detectChanges();
    expect(text(root, '.kiosk__ready')).toBe('Scan your badge');
  });

  it('keeps scans and asks for a new key when the key is no longer accepted', async () => {
    const send = vi.fn<Send>(() => of<KioskSendResult>({ kind: 'key-refused' }));
    const { fixture, root } = await open({ send });

    await scanWithScanner(fixture, 'AAAABBBBCCCC');
    await settle(fixture);

    expect(text(root, '.alert__title')).toBe("This kiosk's key isn't accepted");
    expect(text(root, '.kiosk__waiting')).toBe('1 scan waiting to send');
    buttonNamed(root, 'Enter a new key').click();
    await settle(fixture);
    expect(root.querySelector('input[name="key"]')).not.toBeNull();
    // The waiting scan is still on the tablet for the new key to send.
    expect(JSON.parse(localStorage.getItem('constructopx.kiosk.queue')!)).toHaveLength(1);
  });

  it('remembers whether to beep, and shows nothing but the scan and the time', async () => {
    const { fixture, root } = await open();

    buttonNamed(root, 'Beep: off').click();
    await settle(fixture);
    expect(localStorage.getItem('constructopx.kiosk.beep')).toBe('on');
    expect(buttonNamed(root, 'Beep: on').getAttribute('aria-pressed')).toBe('true');
    expect(root.querySelector('nav')).toBeNull();
    expect(root.querySelectorAll('input')).toHaveLength(2);
  });
});
