import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionState } from '../../core/session/session-state';
import { formatDate } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Button } from '../../shared/ui/button/button';
import { Icon } from '../../shared/ui/icon/icon';
import { ProjectContext } from './project-context';
import { SubProjectsPanel } from './sub-projects-panel';

/** The default tab of a project: its details, and its sub-projects. */
@Component({
  selector: 'app-project-overview-tab',
  imports: [RouterLink, Button, Icon, SubProjectsPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stack">
      <section class="card" aria-labelledby="details-heading">
        <div class="panel-head">
          <h2 id="details-heading" class="card__title">Details</h2>
          @if (context.canManage()) {
            <a appButton [routerLink]="['/projects', context.project().id, 'edit']">
              <app-icon name="pencil" [size]="18" />
              Edit project
            </a>
          }
        </div>
        <dl class="def-list">
          @for (item of details(); track item.label) {
            <div>
              <dt>{{ item.label }}</dt>
              <dd [class.text-muted]="item.empty">{{ item.value }}</dd>
            </div>
          }
        </dl>
      </section>

      <app-sub-projects-panel />
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .text-muted {
      font-weight: 400;
    }
  `,
})
export class ProjectOverviewTab {
  protected readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);

  protected readonly details = computed(() => {
    const project = this.context.project();
    const row = (label: string, value: string | null) => ({
      label,
      value: value ?? 'Not set',
      empty: value === null,
    });
    return [
      row('Client', project.client),
      row('Location', project.location),
      row(
        'Contract price',
        project.contractPriceMinor === null
          ? null
          : formatMoney(project.contractPriceMinor, this.session.currencyCode()),
      ),
      row('Start date', formatDate(project.startDate)),
      row('Target end date', project.targetEndDate ? formatDate(project.targetEndDate) : null),
    ];
  });
}
