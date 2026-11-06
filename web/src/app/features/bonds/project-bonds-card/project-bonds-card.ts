import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { Button } from '../../../shared/ui/button/button';
import { Icon } from '../../../shared/ui/icon/icon';
import { BondFormDialog } from '../bond-form-dialog';
import { BondRows } from '../bond-rows';
import { BondsApi } from '../bonds-api';

/**
 * A project's bonds, for its Billing tab: the Active ones with their dates, and "Add bond" for people who manage bonds on a project that
 * is not archived. It shows nothing at all, not even a heading, when the project has no bonds and the person cannot add one, and it
 * stays out of the way when loading or when the list cannot be read (the tab around it carries on). A bond is a record only: it is not
 * counted in any cost or bill.
 *
 *     <app-project-bonds-card [projectId]="project.id" [projectArchived]="project.status === 'Archived'" />
 */
@Component({
  selector: 'app-project-bonds-card',
  imports: [RouterLink, Button, Icon, BondRows, BondFormDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible()) {
      <section class="card" aria-labelledby="project-bonds-heading">
        <div class="card__head">
          <h2 id="project-bonds-heading" class="card__title">Bonds</h2>
          @if (canAdd()) {
            <button appButton type="button" (click)="adding.set(true)">
              <app-icon name="plus" [size]="18" />
              Add bond
            </button>
          }
        </div>
        @if (active().length > 0) {
          <app-bond-rows [items]="active()" [showProject]="false" />
        } @else {
          <p class="text-small text-muted">No bond is active on this project.</p>
        }
        @if (closedCount() > 0 || archivedNote()) {
          <p class="text-small text-muted">
            @if (archivedNote()) {
              This project is archived, so bonds can't be added or changed.
            }
            @if (closedCount() > 0) {
              <a [routerLink]="['/bonds']" [queryParams]="{ project: projectId(), state: 'All' }"
                >See every bond on this project</a
              >
            }
          </p>
        }
      </section>
    }

    @if (adding()) {
      <app-bond-form-dialog
        [projectId]="projectId()"
        (saved)="added()"
        (dismissed)="adding.set(false)"
        (reloaded)="adding.set(false)"
      />
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .card {
      display: grid;
      gap: var(--space-3);
      padding: var(--space-4);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
    }

    .card__head {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2) var(--space-3);
    }

    .card__title {
      font-size: var(--text-lg);
    }
  `,
})
export class ProjectBondsCard {
  /** The project whose bonds are shown. */
  readonly projectId = input.required<string>();
  /** The project is archived: its bonds can be read but not added to. */
  readonly projectArchived = input(false);

  private readonly api = inject(BondsApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly adding = signal(false);

  /** A person who cannot read bonds is never asked for them: the card is simply absent. */
  private readonly canRead = this.session.permit(Permission.bondsRead);
  private readonly canManage = this.session.permit(Permission.bondsManage);

  protected readonly list = rxResource({
    params: () => (this.canRead() ? this.projectId() : undefined),
    stream: ({ params }) => this.api.forProject(params),
  });

  private readonly items = computed(() => (this.list.hasValue() ? this.list.value() : []));
  protected readonly active = computed(() =>
    this.items().filter((item) => item.status === 'Active'),
  );
  protected readonly closedCount = computed(() => this.items().length - this.active().length);
  protected readonly canAdd = computed(() => this.canManage() && !this.projectArchived());
  protected readonly archivedNote = computed(
    () => this.projectArchived() && this.items().length > 0,
  );

  /** Nothing (not even a heading) when there is nothing to show and nothing the person can do. A failed read shows nothing too. */
  protected readonly visible = computed(() => {
    if (!this.canRead() || this.list.error() || !this.list.hasValue()) return false;
    return this.items().length > 0 || this.canAdd();
  });

  protected added(): void {
    this.adding.set(false);
    this.toast.success('Bond added.');
    this.list.reload();
  }
}
