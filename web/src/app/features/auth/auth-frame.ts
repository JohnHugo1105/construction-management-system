import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon } from '../../shared/ui/icon/icon';

/** The centred card the pages outside the shell (sign-in, first-time setup) sit in. */
@Component({
  selector: 'app-auth-frame',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="auth">
      <div class="auth__card card">
        <p class="auth__brand">
          <app-icon name="construction" [size]="28" />
          Construction Management
        </p>
        <h1 class="auth__title">{{ title() }}</h1>
        @if (intro()) {
          <p class="auth__intro">{{ intro() }}</p>
        }
        <ng-content />
      </div>
    </main>
  `,
  styles: `
    .auth {
      display: grid;
      place-items: start center;
      min-height: 100dvh;
      padding: var(--space-6) var(--space-4) var(--space-8);
    }

    .auth__card {
      display: grid;
      gap: var(--space-4);
      width: min(100%, 26rem);
      margin-top: var(--space-4);
    }

    .auth__brand {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      color: var(--color-brand-text);
      font-weight: 700;
    }

    .auth__title {
      font-size: var(--text-2xl);
    }

    .auth__intro {
      margin-top: calc(var(--space-2) * -1);
      color: var(--color-text-muted);
    }

    @media (min-width: 48rem) {
      .auth {
        place-items: center;
      }

      .auth__card {
        margin-top: 0;
      }
    }
  `,
})
export class AuthFrame {
  readonly title = input.required<string>();
  readonly intro = input('');
}
