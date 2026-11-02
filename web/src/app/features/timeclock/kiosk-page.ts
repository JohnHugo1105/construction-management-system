import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Alert } from '../../shared/ui/alert/alert';
import { Button } from '../../shared/ui/button/button';
import { KioskApi } from './kiosk-api';
import { KioskFeedback, KioskState } from './kiosk-state';

/** How long an answer stays on screen before the kiosk is ready for the next person. */
export const FEEDBACK_MS = 6_000;

/** How often scans kept on the tablet are sent again. */
export const RESEND_MS = 30_000;

/**
 * The kiosk at the gate (`/kiosk`), outside the signed-in app. A USB or Bluetooth scanner types the badge code and presses Enter into the one
 * field that always has focus; a person whose badge won't scan types the code instead. The answer is the worker's name, large, and "Time in
 * 7:02 am" or "Time out 5:03 pm" in words with an icon (and an optional beep), or a plain refusal. Nothing else about anyone is shown. With no
 * connection the scan is kept on the tablet with its real time and sent later, and the screen says how many wait.
 */
@Component({
  selector: 'app-kiosk-page',
  imports: [Alert, Button],
  providers: [KioskState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'kiosk' },
  template: `
    @if (state.key() === null) {
      <main class="kiosk__setup">
        <h1 class="kiosk__heading">Set up this kiosk</h1>
        <app-alert tone="warning" title="Keep this tablet at the site gate">
          Anyone who can use it can record time in and out for your company. The key is kept on this
          tablet only.
        </app-alert>
        <form class="form" (submit)="saveKey($event)">
          <label class="field">
            <span class="field__label">Kiosk key</span>
            <span class="field__hint" id="kiosk-key-hint"
              >Your administrator sees it once, when they add the kiosk under Administration, Time
              clocks.</span
            >
            <input
              #keyInput
              class="input"
              name="key"
              type="password"
              autocomplete="off"
              spellcheck="false"
              aria-describedby="kiosk-key-hint kiosk-key-error"
              [attr.aria-invalid]="keyError() ? 'true' : null"
              [value]="draftKey()"
              (input)="draftKey.set(keyInput.value)"
            />
            @if (keyError(); as message) {
              <span class="field__error" id="kiosk-key-error">{{ message }}</span>
            }
          </label>
          <button appButton variant="primary" type="submit" [busy]="checking()">
            Start the kiosk
          </button>
        </form>
      </main>
    } @else {
      <main class="kiosk__main">
        <p class="kiosk__clock" aria-hidden="true">{{ clock() }}</p>

        <section
          [class]="'kiosk__answer kiosk__answer--' + tone()"
          aria-live="assertive"
          aria-atomic="true"
        >
          @switch (state.feedback()?.kind) {
            @case ('accepted') {
              @if (accepted(); as answer) {
                <svg class="kiosk__icon" viewBox="0 0 24 24" aria-hidden="true">
                  @if (answer.tone === 'in') {
                    <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4M10 17l5-5-5-5M15 12H3" />
                  } @else {
                    <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9" />
                  }
                </svg>
                <p class="kiosk__name">{{ answer.name }}</p>
                <p class="kiosk__words">{{ answer.words }}</p>
                @if (answer.notPosted) {
                  <p class="kiosk__note">
                    You're not on this site's list today. Your scan is kept; tell your supervisor.
                  </p>
                }
              }
            }
            @case ('already') {
              <p class="kiosk__name">{{ already()?.name }}</p>
              <p class="kiosk__words">{{ already()?.words }}</p>
              <p class="kiosk__note">You don't need to scan again.</p>
            }
            @case ('refused') {
              <svg class="kiosk__icon" viewBox="0 0 24 24" aria-hidden="true">
                <path
                  d="M12 9v4M12 17h.01M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"
                />
              </svg>
              <p class="kiosk__words">{{ message() }}</p>
            }
            @case ('saved') {
              <p class="kiosk__words">{{ message() }}</p>
            }
            @default {
              <p class="kiosk__ready">Scan your badge</p>
            }
          }
        </section>

        <label class="kiosk__scanner">
          <span class="field__label">Badge scanner</span>
          <input
            #scanner
            class="input kiosk__input"
            name="scan"
            autocomplete="off"
            autocapitalize="characters"
            spellcheck="false"
            enterkeyhint="send"
            (keydown.enter)="scanned($event)"
            (blur)="refocusSoon()"
          />
        </label>

        <form class="kiosk__typed" (submit)="typed($event)">
          <label class="field">
            <span class="field__label">Badge won't scan? Type the code</span>
            <input
              #typedInput
              class="input"
              name="code"
              autocomplete="off"
              autocapitalize="characters"
              spellcheck="false"
              placeholder="ABCD-EFGH-JKMN"
              (focus)="typing.set(true)"
              (blur)="typing.set(false); refocusSoon()"
            />
          </label>
          <button appButton type="submit" [busy]="state.sending()">Send code</button>
        </form>

        @if (state.waiting() > 0) {
          <p class="kiosk__waiting" role="status">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 6v6l4 2M12 22a10 10 0 1 1 0-20 10 10 0 0 1 0 20z" />
            </svg>
            {{ state.waitingWords() }}
          </p>
        }

        @if (state.keyRefused()) {
          <app-alert tone="danger" title="This kiosk's key isn't accepted">
            Scans are kept on this tablet. Ask your administrator for a new key, then enter it here.
            <div alertActions>
              <button appButton type="button" (click)="changeKey()">Enter a new key</button>
            </div>
          </app-alert>
        }

        <footer class="kiosk__footer">
          <button
            appButton
            variant="ghost"
            type="button"
            [attr.aria-pressed]="state.beep()"
            (click)="state.toggleBeep()"
          >
            Beep: {{ state.beep() ? 'on' : 'off' }}
          </button>
          <button appButton variant="ghost" type="button" (click)="changeKey()">
            Change kiosk key
          </button>
        </footer>
      </main>
    }
  `,
  styles: `
    :host {
      display: block;
      min-height: 100dvh;
      padding: var(--space-4);
      background: var(--color-bg);
      color: var(--color-text);
    }

    .kiosk__setup,
    .kiosk__main {
      display: grid;
      gap: var(--space-4);
      width: min(40rem, 100%);
      margin: 0 auto;
    }

    .kiosk__heading {
      font-size: var(--text-2xl);
    }

    .kiosk__clock {
      font-size: var(--text-3xl);
      font-weight: 700;
      font-variant-numeric: tabular-nums;
      text-align: center;
    }

    .kiosk__answer {
      display: grid;
      justify-items: center;
      gap: var(--space-2);
      min-height: 14rem;
      padding: var(--space-6) var(--space-4);
      border: 3px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
      text-align: center;
      align-content: center;
    }

    .kiosk__answer--in {
      border-color: var(--color-success);
      background: var(--color-success-soft);
    }

    .kiosk__answer--out {
      border-color: var(--color-info);
      background: var(--color-info-soft);
    }

    .kiosk__answer--refused {
      border-color: var(--color-danger);
      background: var(--color-danger-soft);
    }

    .kiosk__answer--saved {
      border-color: var(--color-warning);
      background: var(--color-warning-soft);
    }

    .kiosk__icon {
      width: 3.5rem;
      height: 3.5rem;
      fill: none;
      stroke: currentColor;
      stroke-width: 2;
      stroke-linecap: round;
      stroke-linejoin: round;
    }

    .kiosk__name {
      font-size: clamp(2rem, 8vw, 3.5rem);
      font-weight: 700;
      line-height: 1.1;
      overflow-wrap: anywhere;
    }

    .kiosk__words {
      font-size: clamp(1.25rem, 5vw, 2rem);
      font-weight: 600;
    }

    .kiosk__ready {
      font-size: clamp(1.5rem, 6vw, 2.5rem);
      font-weight: 600;
      color: var(--color-text-muted);
    }

    .kiosk__note {
      font-size: var(--text-lg);
    }

    .kiosk__scanner,
    .kiosk__typed {
      display: grid;
      gap: var(--space-2);
    }

    .kiosk__input {
      min-height: 3.5rem;
      font-size: var(--text-xl);
      letter-spacing: 0.08em;
    }

    .kiosk__waiting {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--space-2);
      padding: var(--space-3);
      border: 1px solid var(--color-warning-border);
      border-radius: var(--radius-md);
      background: var(--color-warning-soft);
      color: var(--color-warning);
      font-weight: 600;
    }

    .kiosk__waiting svg {
      width: 1.25rem;
      height: 1.25rem;
      fill: none;
      stroke: currentColor;
      stroke-width: 2;
    }

    .kiosk__footer {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: var(--space-2);
    }

    @media (min-width: 768px) {
      .kiosk__typed {
        grid-template-columns: 1fr auto;
        align-items: end;
      }
    }
  `,
})
export class KioskPage {
  protected readonly state = inject(KioskState);
  private readonly api = inject(KioskApi);

  private readonly scanner = viewChild<ElementRef<HTMLInputElement>>('scanner');
  private readonly typedInput = viewChild<ElementRef<HTMLInputElement>>('typedInput');

  protected readonly draftKey = signal('');
  protected readonly keyError = signal<string | null>(null);
  protected readonly checking = signal(false);
  protected readonly typing = signal(false);
  protected readonly now = signal(new Date());

  protected readonly clock = computed(() => {
    const now = this.now();
    const hour = now.getHours();
    return `${hour % 12 === 0 ? 12 : hour % 12}:${String(now.getMinutes()).padStart(2, '0')} ${hour < 12 ? 'am' : 'pm'}`;
  });

  protected readonly accepted = computed(() => this.only('accepted'));
  protected readonly already = computed(() => this.only('already'));
  protected readonly message = computed(() => {
    const feedback = this.state.feedback();
    return feedback?.kind === 'refused' || feedback?.kind === 'saved' ? feedback.message : '';
  });

  /** The answer's colour, which repeats what the words and icon already say. */
  protected readonly tone = computed(() => {
    const feedback = this.state.feedback();
    if (!feedback) return 'ready';
    return feedback.kind === 'accepted' ? feedback.tone : feedback.kind;
  });

  private feedbackTimer: ReturnType<typeof setTimeout> | null = null;
  private audio: AudioContext | null = null;

  constructor() {
    const destroyRef = inject(DestroyRef);
    const tick = setInterval(() => this.now.set(new Date()), 1_000);
    const resend = setInterval(() => void this.state.flush(), RESEND_MS);
    const online = () => void this.state.flush();
    globalThis.addEventListener?.('online', online);
    destroyRef.onDestroy(() => {
      clearInterval(tick);
      clearInterval(resend);
      globalThis.removeEventListener?.('online', online);
      if (this.feedbackTimer) clearTimeout(this.feedbackTimer);
      void this.audio?.close();
    });

    // Every answer clears itself after a few seconds, and beeps when the person turned the beep on.
    effect(() => {
      const feedback = this.state.feedback();
      if (!feedback) return;
      untracked(() => {
        if (this.feedbackTimer) clearTimeout(this.feedbackTimer);
        this.feedbackTimer = setTimeout(() => this.state.clearFeedback(), FEEDBACK_MS);
        if (this.state.beep()) this.sound(feedback);
      });
    });

    afterNextRender(() => {
      this.focusScanner();
      void this.state.flush();
    });
  }

  protected async saveKey(event: Event): Promise<void> {
    event.preventDefault();
    const key = this.draftKey().trim();
    if (key === '') {
      this.keyError.set('Enter the kiosk key.');
      return;
    }
    if (this.checking()) return;
    this.checking.set(true);
    this.keyError.set(null);
    try {
      const accepted = await firstValueFrom(this.api.checkKey(key));
      if (accepted === false) {
        this.keyError.set(
          "That key wasn't accepted. Check it and try again, or ask your administrator for a new one.",
        );
        return;
      }
      // Accepted, or it can't be checked without a connection: kept, and checked again with the first scan.
      this.state.useKey(key);
      this.draftKey.set('');
      setTimeout(() => this.focusScanner());
    } finally {
      this.checking.set(false);
    }
  }

  protected changeKey(): void {
    this.state.forgetKey();
  }

  protected scanned(event: Event): void {
    event.preventDefault();
    const input = event.target as HTMLInputElement;
    const text = input.value;
    input.value = '';
    void this.state.scan(text);
  }

  protected typed(event: Event): void {
    event.preventDefault();
    const input = this.typedInput()?.nativeElement;
    if (!input || input.value.trim() === '') return;
    const text = input.value;
    input.value = '';
    void this.state.scan(text);
    this.focusScanner();
  }

  /** The scanner field keeps the focus, except while someone types a code by hand. */
  protected refocusSoon(): void {
    setTimeout(() => {
      if (!this.typing()) this.focusScanner();
    }, 150);
  }

  private focusScanner(): void {
    this.scanner()?.nativeElement.focus();
  }

  private only<K extends KioskFeedback['kind']>(
    kind: K,
  ): Extract<KioskFeedback, { kind: K }> | null {
    const feedback = this.state.feedback();
    return feedback?.kind === kind ? (feedback as Extract<KioskFeedback, { kind: K }>) : null;
  }

  /** A short high tone for a scan taken, two low ones for a refusal. Never the only signal. */
  private sound(feedback: KioskFeedback): void {
    try {
      const Context = globalThis.AudioContext;
      if (!Context) return;
      this.audio ??= new Context();
      const tones =
        feedback.kind === 'accepted' || feedback.kind === 'already' ? [880] : [220, 220];
      tones.forEach((frequency, index) => {
        const oscillator = this.audio!.createOscillator();
        oscillator.frequency.value = frequency;
        oscillator.connect(this.audio!.destination);
        const start = this.audio!.currentTime + index * 0.2;
        oscillator.start(start);
        oscillator.stop(start + 0.12);
      });
    } catch {
      // No sound on this device: the words and icon still say it.
    }
  }
}
