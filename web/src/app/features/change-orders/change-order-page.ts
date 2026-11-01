import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate, formatDateTime } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { ConfirmDialog } from '../../shared/ui/dialog/confirm-dialog';
import { Icon } from '../../shared/ui/icon/icon';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { formatQuantity } from '../costing/costing-labels';
import { ReasonDialog } from '../costing/ui/reason-dialog';
import { ProjectContext } from '../projects/project-context';
import { ChangeOrderFormDialog } from './change-order-form-dialog';
import { ChangeOrderLineDialog } from './change-order-line-dialog';
import {
  REASON_LABELS,
  allowedActions,
  effectSummary,
  governmentLimitNotice,
  signedMoney,
  statusLook,
} from './change-order-presentation';
import { ChangeOrdersApi } from './change-orders-api';
import { ChangeOrder, ChangeOrderLine } from './change-orders.models';

type ReasonAction = 'reject' | 'void' | 'discard';

interface LineRow {
  readonly line: ChangeOrderLine;
  readonly kind: string;
  readonly detail: string;
  readonly effect: string;
}

/** The words of each reason dialog, so the three read alike. */
const REASON_DIALOGS: Record<
  ReasonAction,
  { title: string; message: string; label: string; required: string; confirm: string }
> = {
  reject: {
    title: 'Reject this change order?',
    message: 'Nothing in the budget or the price changes. It stays on record as rejected.',
    label: 'Why is it rejected?',
    required: 'Say why this change order is rejected.',
    confirm: 'Reject change order',
  },
  void: {
    title: 'Void this change order?',
    message:
      'Every line is reversed: added lines are removed, revised lines get their old quantity and cost back, omitted lines return, and the contract price moves back.',
    label: 'Why is it voided?',
    required: 'Say why this change order is voided.',
    confirm: 'Void change order',
  },
  discard: {
    title: 'Discard this change order?',
    message: 'Nothing in the budget changes. It stays on record as discarded.',
    label: 'Why is it discarded?',
    required: 'Say why this change order is discarded.',
    confirm: 'Discard change order',
  },
};

/**
 * One change order: what it changes line by line, its effect on the budget and the contract price, who did what, and the one next
 * step the person may take (submit, approve or reject, void). Lines are added and edited one at a time in a dialog while a draft.
 */
@Component({
  selector: 'app-change-order-page',
  imports: [
    RouterLink,
    Alert,
    ApiErrorAlert,
    Button,
    ConfirmDialog,
    Icon,
    Skeleton,
    StatusChip,
    ReasonDialog,
    ChangeOrderFormDialog,
    ChangeOrderLineDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './change-order-page.scss',
  templateUrl: './change-order-page.html',
})
export class ChangeOrderPage {
  readonly orderId = input.required<string>();
  /** Something changed the budget or the price: the tab refreshes its figures. */
  readonly changed = output<void>();

  private readonly api = inject(ChangeOrdersApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);
  protected readonly context = inject(ProjectContext);

  protected readonly order = signal<ChangeOrder | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly actionError = signal<ApiError | null>(null);
  protected readonly busy = signal<'submit' | 'approve' | 'remove' | null>(null);
  protected readonly editingDetails = signal(false);
  protected readonly lineDialog = signal<{ line: ChangeOrderLine | null } | null>(null);
  protected readonly approving = signal(false);
  protected readonly reasonAction = signal<ReasonAction | null>(null);
  protected readonly reasonWords = computed(() => {
    const action = this.reasonAction();
    return action ? REASON_DIALOGS[action] : null;
  });

  private readonly mayManage = this.session.permit(Permission.changeOrdersManage);
  private readonly mayApprove = this.session.permit(Permission.changeOrdersApprove);

  protected readonly view = computed(() => {
    const order = this.order();
    if (!order) return null;
    const currency = this.session.currencyCode();
    const zone = this.session.timeZoneId();
    const actions = allowedActions(
      order.status,
      { manage: this.mayManage(), approve: this.mayApprove() },
      this.context.isArchived(),
    );
    const trail = [
      `Started by ${order.createdBy ?? 'someone'} on ${formatDateTime(order.createdAtUtc, zone)}`,
      order.submittedAtUtc
        ? `submitted by ${order.submittedBy ?? 'someone'} on ${formatDateTime(order.submittedAtUtc, zone)}`
        : null,
      order.decidedAtUtc
        ? `${order.status === 'Rejected' || order.voidedFromStatus === 'Submitted' ? 'rejected' : 'approved'} by ${order.decidedBy ?? 'someone'} on ${formatDateTime(order.decidedAtUtc, zone)}`
        : null,
      order.voidedAtUtc
        ? `${order.voidedFromStatus === 'Approved' ? 'voided' : 'discarded'} by ${order.voidedBy ?? 'someone'} on ${formatDateTime(order.voidedAtUtc, zone)}`
        : null,
    ].filter((part): part is string => part !== null);
    return {
      order,
      title: order.number ?? 'Draft change order',
      status: statusLook(order.status, order.voidedFromStatus),
      reason: REASON_LABELS[order.reason],
      budget: signedMoney(order.budgetEffectGrossMinor, currency),
      budgetNet: signedMoney(order.budgetEffectNetMinor, currency),
      price:
        order.contractPriceDeltaMinor === 0
          ? null
          : signedMoney(order.contractPriceDeltaMinor, currency),
      estimate: order.status === 'Draft' || order.status === 'Submitted',
      rows: order.lines.map((line) => this.row(line)),
      trail: trail.join(', ') + '.',
      actions: {
        ...actions,
        // Only the newest approved order can be voided; the page says which one stands in the way instead of offering the button.
        void: actions.void && order.voidBlockedBy === null,
      },
      blockedBy: order.status === 'Approved' ? order.voidBlockedBy : null,
      governmentNotice: governmentLimitNotice(order.governmentLimit),
      approveMessage: `The lines are applied to the budget now: ${effectSummary(
        order.budgetEffectGrossMinor,
        order.contractPriceDeltaMinor,
        currency,
      )}. It can be voided later, which reverses it.`,
    };
  });

  constructor() {
    effect(() => {
      const id = this.orderId();
      untracked(() => void this.load(id));
    });
  }

  protected async load(id = this.orderId()): Promise<void> {
    this.loadError.set(null);
    try {
      this.order.set(await firstValueFrom(this.api.get(id)));
    } catch (failure) {
      this.loadError.set(asApiError(failure));
    }
  }

  protected backQuery(): Record<string, null> {
    return { order: null };
  }

  protected accept(order: ChangeOrder): void {
    this.order.set(order);
    this.actionError.set(null);
  }

  protected detailsSaved(order: ChangeOrder): void {
    this.editingDetails.set(false);
    this.accept(order);
  }

  protected lineSaved(order: ChangeOrder): void {
    this.lineDialog.set(null);
    this.accept(order);
  }

  protected async submit(): Promise<void> {
    const order = this.order();
    if (!order) return;
    const result = await this.run('submit', this.api.submit(order.id, order.version));
    if (result) this.toast.success(`Submitted as ${result.number}. It now waits for approval.`);
  }

  protected async approve(): Promise<void> {
    const order = this.order();
    if (!order) return;
    const result = await this.run('approve', this.api.approve(order.id, null, order.version));
    this.approving.set(false);
    if (result) {
      this.toast.success(
        `${result.number} approved. The budget and the contract price are updated.`,
      );
      this.changed.emit();
    }
  }

  protected async removeLine(line: ChangeOrderLine): Promise<void> {
    const order = this.order();
    if (!order) return;
    const result = await this.run('remove', this.api.removeLine(order.id, line.id, order.version));
    if (result) this.toast.success(`Line removed from the change order.`);
  }

  protected readonly performReason = (reason: string): Observable<ChangeOrder> => {
    const order = this.order();
    const action = this.reasonAction();
    if (!order || !action) throw new Error('No change order action is open.');
    return action === 'reject'
      ? this.api.reject(order.id, reason, order.version)
      : action === 'void'
        ? this.api.void(order.id, reason, order.version)
        : this.api.discard(order.id, reason, order.version);
  };

  protected async reasonDone(): Promise<void> {
    const action = this.reasonAction();
    this.reasonAction.set(null);
    await this.load();
    const number = this.order()?.number;
    if (action === 'void') {
      this.toast.success(`${number} voided. Its changes to the budget and the price are reversed.`);
      this.changed.emit();
    } else if (action === 'reject') this.toast.success(`${number} rejected.`);
    else this.toast.success('Change order discarded.');
  }

  protected async reasonOutdated(): Promise<void> {
    this.reasonAction.set(null);
    await this.load();
  }

  private async run(
    action: 'submit' | 'approve' | 'remove',
    request: Observable<ChangeOrder>,
  ): Promise<ChangeOrder | null> {
    if (this.busy()) return null;
    this.busy.set(action);
    this.actionError.set(null);
    try {
      const result = await firstValueFrom(request);
      this.accept(result);
      return result;
    } catch (failure) {
      this.actionError.set(asApiError(failure));
      return null;
    } finally {
      this.busy.set(null);
    }
  }

  private row(line: ChangeOrderLine): LineRow {
    const currency = this.session.currencyCode();
    const amount = (quantity: number | null, cost: number | null) =>
      quantity === null || cost === null
        ? ''
        : `${formatQuantity(quantity)} ${line.unit} × ${formatMoney(cost, currency)}`;
    switch (line.kind) {
      case 'Add':
        return {
          line,
          kind: 'Add',
          detail: `${amount(line.quantity, line.unitCostMinor)} = ${formatMoney(line.grossMinor, currency)} with VAT · ${formatDate(line.entryDate)}`,
          effect: signedMoney(line.effectGrossMinor, currency),
        };
      case 'Revise':
        return {
          line,
          kind: 'Revise',
          detail: `From ${amount(line.previousQuantity, line.previousUnitCostMinor)} to ${amount(line.quantity, line.unitCostMinor)}`,
          effect: signedMoney(line.effectGrossMinor, currency),
        };
      case 'Omit':
        return {
          line,
          kind: 'Omit',
          detail: `Removes ${amount(line.quantity, line.unitCostMinor)} = ${formatMoney(line.grossMinor, currency)} with VAT`,
          effect: signedMoney(line.effectGrossMinor, currency),
        };
    }
  }
}
