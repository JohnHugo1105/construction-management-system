import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate } from '../../shared/presentation/business-time';
import { Button } from '../../shared/ui/button/button';
import { AddCaseDialog } from './add-case-dialog';
import { WorkerDetailState } from './worker-detail-state';
import { WorkerDetail } from './workforce.models';

/** Incident and disciplinary notes. They are added and never edited or removed, so there is only an "Add a case" action. */
@Component({
  selector: 'app-worker-cases-panel',
  imports: [Button, AddCaseDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (canAdd() || cases().length > 0) {
      <section class="card" aria-labelledby="cases-heading">
        <div class="panel-head">
          <h2 id="cases-heading" class="card__title">Cases</h2>
          @if (canAdd()) {
            <button appButton type="button" (click)="adding.set(true)">Add a case</button>
          }
        </div>

        @if (cases().length === 0) {
          <p class="text-muted">No cases recorded.</p>
        } @else {
          <ul class="cases">
            @for (item of cases(); track item.id) {
              <li>
                <p class="cases__title">{{ item.title }}</p>
                <p class="text-muted text-small">{{ item.when }}{{ item.project }}</p>
                @if (item.remarks) {
                  <p class="cases__remarks">{{ item.remarks }}</p>
                }
              </li>
            }
          </ul>
        }
      </section>
    }

    @if (adding()) {
      <app-add-case-dialog
        [worker]="worker()"
        (added)="added($event)"
        (dismissed)="adding.set(false)"
        (reloaded)="reload()"
      />
    }
  `,
  styles: `
    :host {
      display: block;
    }

    :host:empty {
      display: none;
    }

    .cases {
      display: grid;
      gap: var(--space-4);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .cases li {
      display: grid;
      gap: 2px;
      padding-bottom: var(--space-4);
      border-bottom: 1px solid var(--color-border);
    }

    .cases li:last-child {
      padding-bottom: 0;
      border-bottom: 0;
    }

    .cases__title {
      font-weight: 650;
    }

    .cases__remarks {
      margin-top: var(--space-1);
      white-space: pre-line;
    }
  `,
})
export class WorkerCasesPanel {
  private readonly state = inject(WorkerDetailState);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly worker = this.state.worker;
  protected readonly adding = signal(false);
  protected readonly canAdd = this.session.permit(Permission.workforceManage);

  protected readonly cases = computed(() =>
    this.worker().cases.map((item) => ({
      id: item.id,
      title: item.title,
      when: formatDate(item.caseDate),
      project: item.projectName ? ` · ${item.projectName}` : '',
      remarks: item.remarks,
    })),
  );

  protected added(updated: WorkerDetail): void {
    this.adding.set(false);
    this.state.replace(updated);
    this.toast.success('Case added.');
  }

  protected reload(): void {
    this.adding.set(false);
    void this.state.reload();
  }
}
