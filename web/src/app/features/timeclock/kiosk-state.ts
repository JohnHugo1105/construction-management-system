import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { KioskApi } from './kiosk-api';
import { clockTime, kioskDirection, newId } from './timeclock-presentation';
import { KioskScan, PunchResult } from './timeclock.models';

const KEY_STORAGE = 'constructopx.kiosk.key';
const QUEUE_STORAGE = 'constructopx.kiosk.queue';
const BEEP_STORAGE = 'constructopx.kiosk.beep';

/** The most scans sent in one request (the server takes up to 200). */
const BATCH = 200;

/** The most scans kept on the tablet while it is offline: about a week of a 60-person crew scanning four times a day. */
export const QUEUE_LIMIT = 2_000;

/** What the kiosk shows after a scan, until the next one. Words first; the tone and icon repeat them. */
export type KioskFeedback =
  | {
      readonly kind: 'accepted';
      readonly name: string;
      readonly words: string;
      readonly tone: 'in' | 'out';
      readonly notPosted: boolean;
    }
  | { readonly kind: 'already'; readonly name: string; readonly words: string }
  | { readonly kind: 'refused'; readonly message: string }
  | { readonly kind: 'saved'; readonly message: string };

/** Local storage that never throws: a private window or blocked storage just means nothing is remembered. */
function read(key: string): string | null {
  try {
    return globalThis.localStorage?.getItem(key) ?? null;
  } catch {
    return null;
  }
}

function write(key: string, value: string | null): void {
  try {
    if (value === null) globalThis.localStorage?.removeItem(key);
    else globalThis.localStorage?.setItem(key, value);
  } catch {
    // Nothing to do: the kiosk still works for this visit.
  }
}

function readQueue(): KioskScan[] {
  try {
    const parsed: unknown = JSON.parse(read(QUEUE_STORAGE) ?? '[]');
    return Array.isArray(parsed)
      ? parsed.filter(
          (item): item is KioskScan =>
            typeof item === 'object' &&
            item !== null &&
            typeof (item as KioskScan).clientPunchId === 'string' &&
            typeof (item as KioskScan).workerCode === 'string' &&
            typeof (item as KioskScan).punchedAt === 'string',
        )
      : [];
  } catch {
    return [];
  }
}

/** The time on this tablet, "7:02 am", for a scan kept while offline (the server's answer gives the company's time otherwise). */
function tabletTime(instant: Date): string {
  const hours = String(instant.getHours()).padStart(2, '0');
  const minutes = String(instant.getMinutes()).padStart(2, '0');
  return clockTime(`${hours}:${minutes}`);
}

/**
 * The kiosk at the gate. It keeps the device key on this tablet only, sends each scan at once and shows the answer, and when the server can't be
 * reached keeps the scan here with its real time and sends it later (every 30 seconds, and when the connection comes back). A scan is only ever
 * removed from the tablet once the server has answered it.
 */
@Injectable()
export class KioskState {
  private readonly api = inject(KioskApi);

  readonly key = signal<string | null>(read(KEY_STORAGE));
  readonly queue = signal<readonly KioskScan[]>(readQueue());
  readonly feedback = signal<KioskFeedback | null>(null);
  readonly sending = signal(false);
  /** The server refused the key: scans are kept until a new key is entered. */
  readonly keyRefused = signal(false);
  readonly beep = signal(read(BEEP_STORAGE) === 'on');

  readonly waiting = computed(() => this.queue().length);
  readonly waitingWords = computed(() => {
    const count = this.waiting();
    return count === 1 ? '1 scan waiting to send' : `${count} scans waiting to send`;
  });

  private flushing = false;

  /** Keeps the key on this tablet. */
  useKey(key: string): void {
    const clean = key.trim();
    this.key.set(clean);
    this.keyRefused.set(false);
    write(KEY_STORAGE, clean);
  }

  forgetKey(): void {
    this.key.set(null);
    write(KEY_STORAGE, null);
  }

  toggleBeep(): void {
    this.beep.update((on) => !on);
    write(BEEP_STORAGE, this.beep() ? 'on' : 'off');
  }

  /** A badge was scanned (or a code typed). Answers in words; keeps the scan on the tablet if the server can't be reached. */
  async scan(text: string, now: Date = new Date()): Promise<void> {
    const code = text.trim();
    const key = this.key();
    if (code === '' || key === null) return;
    const scan: KioskScan = {
      clientPunchId: newId(),
      workerCode: code,
      punchedAt: now.toISOString(),
    };

    this.sending.set(true);
    try {
      const sent = await firstValueFrom(this.api.send(key, [scan]));
      if (sent.kind === 'answered') {
        this.keyRefused.set(false);
        this.feedback.set(this.describe(sent.result.results[0]));
      } else {
        this.keep([scan]);
        if (sent.kind === 'key-refused') this.keyRefused.set(true);
        this.feedback.set({
          kind: 'saved',
          message:
            sent.kind === 'key-refused'
              ? `Saved on this tablet at ${tabletTime(now)}. This kiosk's key isn't accepted any more: ask your administrator for a new key.`
              : `Saved on this tablet at ${tabletTime(now)}. It will send itself when the connection is back.`,
        });
      }
    } finally {
      this.sending.set(false);
    }
    // A scan that went through means the connection is back: send what waits.
    if (this.waiting() > 0 && !this.keyRefused()) void this.flush();
  }

  /** Sends what waits on the tablet. Scans the server answered (whatever the answer) leave the tablet; the rest stay. */
  async flush(): Promise<void> {
    const key = this.key();
    if (this.flushing || key === null || this.queue().length === 0) return;
    this.flushing = true;
    try {
      while (this.queue().length > 0) {
        const batch = this.queue().slice(0, BATCH);
        const sent = await firstValueFrom(this.api.send(key, batch));
        if (sent.kind === 'key-refused') {
          this.keyRefused.set(true);
          return;
        }
        if (sent.kind === 'unreachable') return;
        this.keyRefused.set(false);
        const answered = new Set(sent.result.results.map((result) => result.clientPunchId));
        this.setQueue(this.queue().filter((item) => !answered.has(item.clientPunchId)));
        if (answered.size === 0) return;
      }
    } finally {
      this.flushing = false;
    }
  }

  clearFeedback(): void {
    this.feedback.set(null);
  }

  private keep(scans: readonly KioskScan[]): void {
    // At the limit the oldest scans give way: a tablet offline for weeks needs a person, not more storage.
    this.setQueue([...this.queue(), ...scans].slice(-QUEUE_LIMIT));
  }

  private setQueue(queue: readonly KioskScan[]): void {
    this.queue.set(queue);
    write(QUEUE_STORAGE, JSON.stringify(queue));
  }

  private describe(result: PunchResult | undefined): KioskFeedback {
    if (!result) return { kind: 'refused', message: "This scan couldn't be read. Scan again." };
    if (result.outcome === 'Rejected')
      return {
        kind: 'refused',
        message: result.reason ?? "This scan couldn't be read. Scan again.",
      };
    const direction = kioskDirection(result.direction);
    const time = clockTime(result.localTime);
    if (result.outcome === 'AlreadyRecorded')
      return {
        kind: 'already',
        name: result.displayName ?? '',
        words: `Already recorded: ${direction.words.toLowerCase()} ${time}`,
      };
    return {
      kind: 'accepted',
      name: result.displayName ?? '',
      words: `${direction.words} ${time}`,
      tone: direction.tone,
      notPosted: result.notPostedHere,
    };
  }
}
