import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Menu } from '../../shared/ui/menu/menu';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { BondActionDialog, BondStep } from './bond-action-dialog';
import { BondFormDialog } from './bond-form-dialog';
import { BondState } from './bond-state';
import {
  bondActions,
  bondLook,
  bondTitle,
  directionWords,
  expiredAgo,
  expiresIn,
  historyLine,
  needsAttention,
} from './bonds-presentation';
import { Bond } from './bonds.models';

type OpenDialog = BondStep | 'edit';

/**
 * One bond: where it stands, the amount and the date it is valid until, who gave it to whom, and its history. The one next step leads
 * when the bond has lapsed or is about to (Extend bond); the rest sit beside it, with calling and voiding folded under "More". A person
 * who may only read, or a bond on an archived project, sees no buttons. A bond changes no money figure anywhere, and the page says so.
 */
@Component({
  selector: 'app-bond-page',
  imports: [
    RouterLink,
    PageHeader,
    Alert,
    ApiErrorAlert,
    Button,
    Menu,
    Skeleton,
    StatusChip,
    BondActionDialog,
    BondFormDialog,
  ],
  providers: [BondState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="title()" backLabel="All bonds" [backLink]="['/bonds']" />

    @switch (state.status()) {
      @case ('loading') {
        <app-skeleton [lines]="4" label="Loading bond" />
      }
      @case ('error') {
        <div class="stack">
          <app-api-error-alert
            [error]="state.error()"
            [allowRetry]="!state.error()?.isNotFound"
            reloadLabel="Try again"
            (reload)="reload()"
          />
          <p><a appButton routerLink="/bonds">Back to bonds</a></p>
        </div>
      }
      @case ('ready') {
        @if (view(); as info) {
          <div class="stack" [attr.aria-busy]="state.refreshing()">
            <div class="head">
              <app-status-chip
                [label]="info.look.label"
                [tone]="info.look.tone"
                [icon]="info.look.icon"
              />
              <p class="head__who">
                {{ info.who }} ·
                <a [routerLink]="['/projects', info.projectId]">{{ info.projectName }}</a>
              </p>
            </div>

            @if (info.archivedNote) {
              <p class="text-small text-muted">{{ info.archivedNote }}</p>
            }
            @if (info.attention) {
              <app-alert tone="warning" [title]="info.attention.title">{{
                info.attention.message
              }}</app-alert>
            }
            @if (info.voidReason) {
              <app-alert tone="info" title="This bond was voided"
                >Reason: {{ info.voidReason }}</app-alert
              >
            }

            <dl class="figures">
              <div class="figures__item figures__item--lead">
                <dt>Amount</dt>
                <dd class="money">{{ info.amount }}</dd>
                <dd class="figures__note">
                  Guaranteed by {{ info.issuer }}. Not counted in any cost or bill.
                </dd>
              </div>
              <div class="figures__item">
                <dt>Valid until</dt>
                <dd>{{ info.validUntil }}</dd>
                @if (info.validNote) {
                  <dd class="figures__note">{{ info.validNote }}</dd>
                }
              </div>
              <div class="figures__item">
                <dt>Issued on</dt>
                <dd>{{ info.issuedOn }}</dd>
              </div>
            </dl>
            <p class="text-small text-muted">Bond number {{ info.number }}</p>
            @if (info.closing) {
              <p class="text-small">{{ info.closing }}</p>
            }
            @if (info.note) {
              <p class="text-small">{{ info.note }}</p>
            }

            @if (actions(); as can) {
              @if (can.extend || can.release || can.edit || can.markCalled || can.voidBond) {
                <div class="actions">
                  @if (can.extend) {
                    <button
                      appButton
                      [variant]="info.attention ? 'primary' : 'secondary'"
                      type="button"
                      (click)="open.set('extend')"
                    >
                      Extend bond
                    </button>
                  }
                  @if (can.release) {
                    <button appButton type="button" (click)="open.set('release')">
                      Release bond
                    </button>
                  }
                  @if (can.edit) {
                    <button appButton type="button" (click)="open.set('edit')">
                      Change details
                    </button>
                  }
                  @if (can.markCalled) {
                    <app-menu label="More" align="end">
                      <button type="button" class="menu__item" (click)="open.set('call')">
                        Mark as called
                      </button>
                      <button type="button" class="menu__item" (click)="open.set('void')">
                        Void bond
                      </button>
                    </app-menu>
                  } @else if (can.voidBond) {
                    <button appButton variant="ghost" type="button" (click)="open.set('void')">
                      Void bond
                    </button>
                  }
                </div>
              }
            }

            <section class="section" aria-labelledby="history-heading">
              <h2 id="history-heading" class="section__title">History</h2>
              <ul class="history">
                @for (line of info.history; track line.id) {
                  <li class="history__item">
                    <p>{{ line.title }}</p>
                    <p class="text-small text-muted">{{ line.meta }}</p>
                    @if (line.note) {
                      <p class="text-small">{{ line.note }}</p>
                    }
                  </li>
                }
              </ul>
            </section>
          </div>
        }
      }
    }

    @if (state.status() === 'ready') {
      @switch (open()) {
        @case ('edit') {
          <app-bond-form-dialog
            [bond]="state.bond()"
            (saved)="done($event, 'Bond details saved.')"
            (dismissed)="open.set(null)"
            (reloaded)="reload()"
          />
        }
        @case ('extend') {
          <app-bond-action-dialog
            step="extend"
            [bond]="state.bond()"
            (done)="done($event, 'Bond extended.')"
            (dismissed)="open.set(null)"
            (reloaded)="reload()"
          />
        }
        @case ('release') {
          <app-bond-action-dialog
            step="release"
            [bond]="state.bond()"
            (done)="done($event, 'Bond released.')"
            (dismissed)="open.set(null)"
            (reloaded)="reload()"
          />
        }
        @case ('call') {
          <app-bond-action-dialog
            step="call"
            [bond]="state.bond()"
            (done)="done($event, 'Bond marked as called.')"
            (dismissed)="open.set(null)"
            (reloaded)="reload()"
          />
        }
        @case ('void') {
          <app-bond-action-dialog
            step="void"
            [bond]="state.bond()"
            (done)="done($event, 'Bond voided.')"
            (dismissed)="open.set(null)"
            (reloaded)="reload()"
          />
        }
      }
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-2) var(--space-3);
    }

    .head__who {
      min-width: 0;
      color: var(--color-text-muted);
      overflow-wrap: anywhere;
    }

    .figures {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--space-3);
      margin: 0;
    }

    .figures__item {
      display: grid;
      align-content: start;
      gap: 2px;
      padding: var(--space-3) var(--space-4);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);

      dt {
        font-size: var(--text-sm);
        color: var(--color-text-muted);
      }

      dd {
        margin: 0;
        font-size: var(--text-lg);
        font-weight: 650;
        overflow-wrap: anywhere;
      }

      .figures__note {
        font-size: var(--text-sm);
        font-weight: 400;
        color: var(--color-text-muted);
      }
    }

    .figures__item--lead {
      grid-column: 1 / -1;

      dd:not(.figures__note) {
        font-size: var(--text-2xl);
      }
    }

    @media (min-width: 48rem) {
      .figures {
        grid-template-columns: repeat(3, minmax(0, 1fr));
      }

      .figures__item--lead {
        grid-column: span 1;
      }
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
    }

    @media (max-width: 47.99rem) {
      .actions .btn--primary {
        flex: 1 1 100%;
      }
    }

    .section {
      display: grid;
      gap: var(--space-3);
    }

    .section__title {
      font-size: var(--text-lg);
    }

    .history {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .history__item {
      display: grid;
      gap: 2px;
      padding: var(--space-2) 0;
      border-bottom: 1px solid var(--color-border);
      overflow-wrap: anywhere;
    }
  `,
})
export class BondPage {
  /** Bound from the route (`/bonds/:id`). */
  readonly id = input.required<string>();

  protected readonly state = inject(BondState);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly open = signal<OpenDialog | null>(null);

  private readonly ready = computed(() =>
    this.state.status() === 'ready' ? this.state.bond() : null,
  );

  protected readonly title = computed(() => {
    const value = this.ready();
    return value ? bondTitle(value) : 'Bond';
  });

  protected readonly actions = computed(() => {
    const value = this.ready();
    return value ? bondActions(value, this.session.can(Permission.bondsManage)) : null;
  });

  protected readonly view = computed(() => {
    const value = this.ready();
    if (!value) return null;
    const zone = this.session.timeZoneId();
    return {
      look: bondLook(value),
      who: directionWords(value),
      projectId: value.projectId,
      projectName: value.projectName,
      archivedNote: value.projectArchived
        ? 'This project is archived, so this bond can be read but not changed. Restore the project to change it.'
        : '',
      attention: attention(value),
      voidReason: value.voidReason,
      amount: formatMoney(value.amountMinor, value.currencyCode),
      issuer: value.issuerName,
      number: value.bondNumber,
      validUntil: formatDate(value.validUntil),
      validNote: validNote(value),
      issuedOn: formatDate(value.issuedOn),
      closing: closing(value),
      note: value.note,
      history: value.history.map((event) => historyLine(event, zone)),
    };
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.state.load(id));
    });
  }

  protected done(bond: Bond, message: string): void {
    this.open.set(null);
    this.state.replace(bond);
    this.toast.success(message);
  }

  protected reload(): void {
    this.open.set(null);
    void this.state.reload();
  }
}

/** Under the valid-until date: how long is left, or how long ago it passed. Only for an Active bond. */
function validNote(bond: Bond): string {
  if (bond.status !== 'Active' || bond.daysLeft === null) return '';
  if (bond.expiry === 'Expired') return expiredAgo(bond.daysLeft);
  return bond.expiry === 'ExpiringSoon' ? expiresIn(bond.daysLeft) : '';
}

/** The warning above an Active bond that has lapsed or is about to. */
function attention(bond: Bond): { title: string; message: string } | null {
  if (!needsAttention(bond) || bond.daysLeft === null) return null;
  const date = formatDate(bond.validUntil);
  return bond.expiry === 'Expired'
    ? {
        title: 'This bond has expired',
        message: `It was valid until ${date} and is still active. Extend it if the bank or surety renewed it, or release it if it is no longer needed.`,
      }
    : {
        title: expiresIn(bond.daysLeft),
        message: `It is valid until ${date}. Extend it if it needs to stay in force.`,
      };
}

/** How a closed bond ended, in one line. */
function closing(bond: Bond): string {
  if (bond.status === 'Released')
    return `Released on ${formatDate(bond.releasedOn)}.${bond.releaseNote ? ` ${bond.releaseNote}` : ''}`;
  if (bond.status === 'Called')
    return `Called on ${formatDate(bond.calledOn)}. ${bond.callNote ?? ''}`.trim();
  return '';
}
