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
import { formatDate, formatDateTime } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { formatBps } from '../../shared/presentation/percent';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { ConfirmActionDialog, ReasonDialog, VariationDialog } from './action-dialogs';
import { ClaimDialog } from './claim-dialog';
import { PaymentDialog } from './payment-dialog';
import {
  NextStep,
  SUBCONTRACT_STATUS,
  VAT_WORDS,
  claimLook,
  claimName,
  methodLabel,
  nextStep,
  subcontractActions,
  waitingLine,
} from './subcontract-presentation';
import { SubcontractState } from './subcontract-state';
import { SubcontractsApi } from './subcontracts-api';
import { Subcontract, SubcontractClaim, SubcontractPayment } from './subcontracts.models';

type OpenDialog =
  | { readonly kind: 'award' | 'complete' | 'cancel' | 'variation' | 'release' }
  | { readonly kind: 'claim'; readonly claim: SubcontractClaim | null }
  | { readonly kind: 'certify' | 'discard' | 'void-claim' | 'pay'; readonly claim: SubcontractClaim }
  | { readonly kind: 'void-payment'; readonly payment: SubcontractPayment };

/**
 * One subcontract: who does the work and where, the money position (revised sum, certified, still to claim, owed, retention held),
 * the one next step for this person, then the claims, payments and variations. What a person can do follows what they hold and the
 * subcontract's state; a person who may only read sees the figures and no buttons. Every figure is the server's.
 */
@Component({
  selector: 'app-subcontract-page',
  imports: [
    RouterLink,
    PageHeader,
    Alert,
    ApiErrorAlert,
    Button,
    Skeleton,
    StatusChip,
    ConfirmActionDialog,
    ReasonDialog,
    VariationDialog,
    ClaimDialog,
    PaymentDialog,
  ],
  providers: [SubcontractState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './subcontract-page.html',
  styleUrl: './subcontract-page.scss',
})
export class SubcontractPage {
  /** Bound from the route (`/subcontracts/:id`). */
  readonly id = input.required<string>();

  protected readonly state = inject(SubcontractState);
  private readonly api = inject(SubcontractsApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly open = signal<OpenDialog | null>(null);

  private readonly ready = computed(() => (this.state.status() === 'ready' ? this.state.subcontract() : null));

  protected readonly title = computed(() => {
    const value = this.ready();
    return value ? `${value.number} ${value.title}` : 'Subcontract';
  });

  protected readonly actions = computed(() => {
    const value = this.ready();
    return value
      ? subcontractActions(value, {
          manage: this.session.can(Permission.subcontractsManage),
          approve: this.session.can(Permission.subcontractsApprove),
        })
      : null;
  });

  protected readonly step = computed<NextStep | null>(() => {
    const value = this.ready();
    const actions = this.actions();
    return value && actions ? nextStep(value, actions) : null;
  });

  protected readonly waiting = computed(() => {
    const value = this.ready();
    const actions = this.actions();
    return value && actions ? waitingLine(value, actions, this.step()) : '';
  });

  protected readonly view = computed(() => {
    const value = this.ready();
    if (!value) return null;
    const currency = this.session.currencyCode();
    const zone = this.session.timeZoneId();
    const money = (minor: number) => formatMoney(minor, currency);
    const place = value.subProjectName ? `${value.projectName}, ${value.subProjectName}` : value.projectName;
    const figures: { label: string; amount: string; note?: string; lead?: boolean }[] = [];
    const vat = VAT_WORDS[value.vatTreatment];
    if (value.status === 'Draft' || value.status === 'Cancelled') {
      figures.push({ label: 'Contract sum', amount: money(value.revisedSumMinor), note: vat, lead: true });
    } else {
      figures.push({
        label: 'Certified',
        amount: money(value.certifiedWorkMinor),
        note: value.percentCertifiedBps === null ? vat : `${formatBps(value.percentCertifiedBps)} of ${money(value.revisedSumMinor)} ${vat}`,
        lead: true,
      });
      if (value.remainingWorkMinor > 0 && value.status === 'Active')
        figures.push({ label: 'Still to claim', amount: money(value.remainingWorkMinor), note: vat });
      // Show what is still owed; once everything certified is paid, say what was paid instead of a zero.
      if (value.outstandingMinor > 0) figures.push({ label: 'Owed', amount: money(value.outstandingMinor), note: 'on certified claims' });
      else if (value.paidOnClaimsMinor > 0) figures.push({ label: 'Paid', amount: money(value.paidOnClaimsMinor), note: 'all certified claims' });
      if (value.retentionBalanceMinor > 0)
        figures.push({ label: 'Retention held', amount: money(value.retentionBalanceMinor), note: value.retentionReleasedMinor > 0 ? `${money(value.retentionReleasedMinor)} released` : 'paid at completion' });
      else if (value.retentionReleasedMinor > 0)
        figures.push({ label: 'Retention released', amount: money(value.retentionReleasedMinor), note: 'nothing more held' });
    }
    const terms = [
      `Retention ${formatBps(value.retentionBps)}`,
      `Withholding tax ${formatBps(value.withholdingBps)}`,
      value.vatTreatment === 'Exempt' ? 'No VAT' : `VAT ${formatBps(value.vatRateBps)}`,
    ];
    if (value.variationsMinor !== 0)
      terms.push(`Original sum ${money(value.contractSumMinor)}, variations ${value.variationsMinor > 0 ? '+' : '-'}${money(Math.abs(value.variationsMinor))}`);
    const history: string[] = [];
    if (value.createdByName) history.push(`Drafted by ${value.createdByName} on ${formatDateTime(value.createdAtUtc, zone)}`);
    if (value.awardedAtUtc) history.push(`Awarded by ${value.awardedByName ?? 'someone'} on ${formatDateTime(value.awardedAtUtc, zone)}`);
    if (value.completedAtUtc) history.push(`Completed by ${value.completedByName ?? 'someone'} on ${formatDateTime(value.completedAtUtc, zone)}`);
    if (value.cancelledAtUtc) history.push(`Cancelled by ${value.cancelledByName ?? 'someone'} on ${formatDateTime(value.cancelledAtUtc, zone)}`);
    return {
      status: SUBCONTRACT_STATUS[value.status],
      who: `${value.subcontractorName} · ${place}`,
      projectId: value.projectId,
      figures,
      terms: terms.join(' · '),
      notes: value.notes,
      history,
      cancelReason: value.cancelReason,
      showClaims: value.claims.length > 0 || value.status === 'Active',
      claims: value.claims.map((claim) => ({
        claim,
        name: claimName(claim),
        look: claimLook(claim),
        dates: `${formatDate(claim.claimDate)} · work up to ${formatDate(claim.periodEnd)}`,
        toDate: money(claim.cumulativeWorkMinor),
        thisClaim: money(claim.thisClaimBaseMinor),
        gross: money(claim.grossMinor),
        payable: money(claim.netPayableMinor),
        owed: claim.status === 'Certified' && claim.outstandingMinor > 0 ? money(claim.outstandingMinor) : '',
        voided: claim.status === 'Voided',
        reason: claim.voidReason,
      })),
      payments: value.payments.map((payment) => ({
        payment,
        what: payment.kind === 'RetentionRelease' ? 'Retention released' : `Claim ${payment.claimNumber}`,
        detail: [formatDate(payment.paidOn), methodLabel(payment.method), payment.reference].filter(Boolean).join(' · '),
        amount: money(payment.amountMinor),
        voided: payment.status === 'Voided',
        reason: payment.voidReason,
      })),
      variations: value.variations.map((variation) => ({
        id: variation.id,
        text: `${variation.number}. ${variation.description}`,
        date: formatDate(variation.variationDate),
        amount: `${variation.amountMinor < 0 ? '-' : '+'}${money(Math.abs(variation.amountMinor))}`,
      })),
    };
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.state.load(id));
    });
  }

  /** The claim the primary button already pays, so its card doesn't offer the same step a second time. */
  protected readonly stepClaimId = computed(() => {
    const step = this.step();
    return step?.kind === 'pay' ? step.claimId : null;
  });

  protected runStep(step: NextStep): void {
    const value = this.state.subcontract();
    switch (step.kind) {
      case 'award':
        this.open.set({ kind: 'award' });
        return;
      case 'certify':
      case 'update-claim':
      case 'pay': {
        const claim = value.claims.find((item) => item.id === step.claimId);
        if (!claim) return;
        this.open.set(step.kind === 'update-claim' ? { kind: 'claim', claim } : { kind: step.kind, claim });
        return;
      }
      case 'new-claim':
        this.open.set({ kind: 'claim', claim: null });
        return;
      case 'release-retention':
        this.open.set({ kind: 'release' });
    }
  }

  /** The requests the confirm and reason dialogs run, bound to the subcontract as it is now. */
  protected readonly award = () => this.api.award(this.state.subcontract().id, this.state.subcontract().version);
  protected readonly complete = () => this.api.complete(this.state.subcontract().id, this.state.subcontract().version);
  protected readonly cancel = (reason: string) =>
    this.api.cancel(this.state.subcontract().id, reason, this.state.subcontract().version);
  protected readonly addVariation = (variation: { description: string; amountMinor: number | null; variationDate: string }) =>
    this.api.addVariation(this.state.subcontract().id, variation, this.state.subcontract().version);
  protected certify(claim: SubcontractClaim) {
    return () => this.api.certifyClaim(this.state.subcontract().id, claim.id, this.state.subcontract().version);
  }
  protected discard(claim: SubcontractClaim) {
    return (reason: string) => this.api.discardClaim(this.state.subcontract().id, claim.id, reason, this.state.subcontract().version);
  }
  protected voidClaim(claim: SubcontractClaim) {
    return (reason: string) => this.api.voidClaim(this.state.subcontract().id, claim.id, reason, this.state.subcontract().version);
  }
  protected voidPayment(payment: SubcontractPayment) {
    return (reason: string) =>
      this.api.voidPayment(this.state.subcontract().id, payment.id, reason, this.state.subcontract().version);
  }

  protected certifyMessage(claim: SubcontractClaim): string {
    const currency = this.session.currencyCode();
    return (
      `Certifying posts ${formatMoney(claim.grossMinor, currency)} with VAT to the project's cost and makes ` +
      `${formatMoney(claim.netPayableMinor, currency)} payable to ${this.state.subcontract().subcontractorName}.`
    );
  }

  protected done(subcontract: Subcontract, message: string): void {
    this.open.set(null);
    this.state.replace(subcontract);
    this.toast.success(message);
  }

  protected reload(): void {
    this.open.set(null);
    void this.state.reload();
  }
}
