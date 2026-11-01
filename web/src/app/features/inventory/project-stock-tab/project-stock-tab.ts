import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { Alert } from '../../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Pager } from '../../../shared/ui/pager/pager';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { ProjectContext } from '../../projects/project-context';
import { InventoryApi } from '../inventory-api';
import { CATEGORY, quantityWithUnit } from '../inventory-presentation';
import { InventoryItem, ProjectStock } from '../inventory.models';
import { MoveStockDialog } from '../move-stock-dialog';
import { StockPlaces } from '../stock-places';
import { ProjectStockState } from './project-stock-state';

/**
 * The Stock tab of a project: what is on the project now, each item linking to its page. People who manage stock get a
 * "Move to the warehouse" shortcut on each row. An archived project is read-only for stock coming in, but stock may still be
 * taken off it, so the shortcut stays and the page says so in plain words.
 */
@Component({
  selector: 'app-project-stock-tab',
  imports: [
    RouterLink,
    Alert,
    ApiErrorAlert,
    Button,
    EmptyState,
    Icon,
    Pager,
    Skeleton,
    StatusChip,
    MoveStockDialog,
  ],
  providers: [ProjectStockState, StockPlaces],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-stock-tab.html',
  styleUrl: './project-stock-tab.scss',
})
export class ProjectStockTab {
  protected readonly context = inject(ProjectContext);
  protected readonly state = inject(ProjectStockState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);
  private readonly api = inject(InventoryApi);
  private readonly toast = inject(ToastService);

  protected readonly canManage = this.session.permit(Permission.inventoryManage);

  /** The item whose "Move to the warehouse" dialog is open (fetched when the person asks, so its version is current). */
  protected readonly returning = signal<InventoryItem | null>(null);
  /** The row whose item is being fetched, to show it busy and ignore a second press. */
  protected readonly fetching = signal<string | null>(null);
  protected readonly error = signal<ApiError | null>(null);

  protected readonly rows = computed(() =>
    this.state.items().map((stock) => ({
      stock,
      category: CATEGORY[stock.category],
      about: [stock.brand, stock.serialNumber ? `Serial ${stock.serialNumber}` : null]
        .filter(Boolean)
        .join(' · '),
      quantity: quantityWithUnit(stock.quantity, stock.unit),
    })),
  );

  /** What to say when nothing is on the project: a next step for people who can move stock here, plain facts for the rest. */
  protected readonly emptyMessage = computed(() => {
    if (this.context.isArchived())
      return 'No tools, equipment or machinery are on this archived project.';
    return this.canManage()
      ? 'Move tools, equipment or machinery here from an item in stock.'
      : 'Stock appears here once it is moved to this project.';
  });

  protected async moveToWarehouse(stock: ProjectStock): Promise<void> {
    if (this.fetching() !== null) return;
    this.fetching.set(stock.itemId);
    this.error.set(null);
    try {
      this.returning.set(await firstValueFrom(this.api.get(stock.itemId)));
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.fetching.set(null);
    }
  }

  /** Someone else changed the item while the dialog was open: take its current state and let the person send again. */
  protected async refreshReturning(): Promise<void> {
    const current = this.returning();
    if (!current) return;
    try {
      this.returning.set(await firstValueFrom(this.api.get(current.id)));
    } catch (failure) {
      this.returning.set(null);
      this.error.set(asApiError(failure));
    }
  }

  protected moved(): void {
    this.returning.set(null);
    this.state.reload();
    this.toast.success('Moved to the warehouse.');
  }
}
