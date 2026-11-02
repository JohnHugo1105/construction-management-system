import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Button } from '../../../shared/ui/button/button';
import { Menu } from '../../../shared/ui/menu/menu';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { ChipLook, DocumentActions } from '../documents-presentation';
import { DocumentListItem } from '../documents.models';

/** One document as the tab shows it, already worded by the tab. */
export interface DocumentRowView {
  readonly item: DocumentListItem;
  readonly chip: ChipLook | null;
  /** "Version 2 · PDF, 1.2 MB · Ana Reyes, 3 Dec 2026". */
  readonly meta: string;
  /** "Download" or "Open link". */
  readonly open: string;
  readonly openLabel: string;
  /** A file downloads; a link opens in a new tab. */
  readonly download: boolean;
  readonly href: string;
  /** It has older versions to look at. */
  readonly earlier: boolean;
  readonly can: DocumentActions;
}

export type DocumentRowStep = 'version' | 'versions' | 'details' | 'void';

/**
 * A document's card: its title and chip, its description, its newest version in one line, the download or link as the card's own
 * action, and the rest (a new version, earlier versions, details, void) one step away in "More". A reader sees only what they can use.
 */
@Component({
  selector: 'li[app-document-row]',
  imports: [Button, Menu, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'row-card document' },
  template: `
    <div class="row-card__head">
      <h3 class="row-card__title document__title">{{ row().item.title }}</h3>
      @if (row().chip; as chip) {
        <app-status-chip [label]="chip.label" [tone]="chip.tone" [icon]="chip.icon" />
      }
    </div>
    @if (row().item.description) {
      <p class="text-small document__text">{{ row().item.description }}</p>
    }
    <p class="text-small text-muted document__text">{{ row().meta }}</p>
    <div class="document__actions">
      <a
        appButton
        [href]="row().href"
        [attr.download]="row().download ? '' : null"
        [attr.target]="row().download ? null : '_blank'"
        [attr.rel]="row().download ? null : 'noopener noreferrer'"
        [attr.aria-label]="row().openLabel"
        >{{ row().open }}</a
      >
      @if (row().can.addVersion) {
        <app-menu label="More" align="end" [ariaLabel]="'More actions for ' + row().item.title">
          <button type="button" class="menu__item" (click)="chosen.emit('version')">
            Add a new version
          </button>
          @if (row().earlier) {
            <button type="button" class="menu__item" (click)="chosen.emit('versions')">
              Earlier versions
            </button>
          }
          <button type="button" class="menu__item" (click)="chosen.emit('details')">
            Change details
          </button>
          <button type="button" class="menu__item" (click)="chosen.emit('void')">
            Void document
          </button>
        </app-menu>
      } @else if (row().earlier) {
        <button appButton variant="ghost" type="button" (click)="chosen.emit('versions')">
          Earlier versions
        </button>
      }
    </div>
  `,
  styles: `
    .document__title {
      overflow-wrap: anywhere;
    }

    .document__text {
      overflow-wrap: anywhere;
    }

    .document__actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
      margin-top: var(--space-1);
    }
  `,
})
export class DocumentRow {
  readonly row = input.required<DocumentRowView>();
  readonly chosen = output<DocumentRowStep>();
}
