import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

// Inline SVG icons on a 24 x 24 grid, drawn with strokes in the current text colour. No icon font and nothing fetched.
// Each icon is a list of path strings; add one here and it is available everywhere as <app-icon name="..." />.
const ICON_PATHS = {
  plus: ['M12 5v14', 'M5 12h14'],
  search: ['M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14z', 'M20 20l-3.5-3.5'],
  close: ['M6 6l12 12', 'M18 6L6 18'],
  check: ['M5 12.5l4.5 4.5L19 7.5'],
  'check-circle': ['M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z', 'M8 12.5l3 3 5-6'],
  warning: [
    'M10.3 3.9L2.4 17.5A2 2 0 0 0 4.1 20.5h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z',
    'M12 9.5v4',
    'M12 17h.01',
  ],
  info: ['M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z', 'M12 11v5', 'M12 7.5h.01'],
  error: ['M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z', 'M12 7.5v5', 'M12 16h.01'],
  'minus-circle': ['M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z', 'M8 12h8'],
  'chevron-down': ['M6 9l6 6 6-6'],
  'chevron-left': ['M15 6l-6 6 6 6'],
  'chevron-right': ['M9 6l6 6-6 6'],
  'arrow-left': ['M19 12H5', 'M11 6l-6 6 6 6'],
  more: [
    'M5 10.6a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8z',
    'M12 10.6a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8z',
    'M19 10.6a1.4 1.4 0 1 0 0 2.8 1.4 1.4 0 0 0 0-2.8z',
  ],
  user: ['M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8z', 'M4 20c0-3.3 3.6-6 8-6s8 2.7 8 6'],
  users: [
    'M9 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7z',
    'M2.5 20c0-3 2.9-5 6.5-5s6.5 2 6.5 5',
    'M16 4.3a3.5 3.5 0 0 1 0 6.4',
    'M18 15.2c2 .7 3.5 2.2 3.5 4.8',
  ],
  logout: ['M9 4H5a1 1 0 0 0-1 1v14a1 1 0 0 0 1 1h4', 'M16 8l4 4-4 4', 'M20 12H9'],
  lock: [
    'M6 11h12a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1v-7a1 1 0 0 1 1-1z',
    'M8 11V8a4 4 0 0 1 8 0v3',
  ],
  building: [
    'M4 21V4a1 1 0 0 1 1-1h9a1 1 0 0 1 1 1v17',
    'M15 9h4a1 1 0 0 1 1 1v11',
    'M3 21h18',
    'M8 7h3',
    'M8 11h3',
    'M8 15h3',
  ],
  logo: ['M4 20V10l8-6 8 6v10', 'M9.5 20v-6h5v6'],
  dashboard: ['M4 4h7v7H4z', 'M13 4h7v4h-7z', 'M13 10h7v10h-7z', 'M4 13h7v7H4z'],
  payroll: [
    'M3 6h18v12H3z',
    'M12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5z',
    'M6.5 9.5h.01',
    'M17.5 14.5h.01',
  ],
  stock: ['M3.5 7.5L12 3l8.5 4.5v9L12 21l-8.5-4.5z', 'M3.5 7.5L12 12l8.5-4.5', 'M12 12v9'],
  cart: ['M3 4h2.5l2 11h10.5l2-8H6.5', 'M9.5 19.5a1 1 0 1 0 0 .01', 'M16.5 19.5a1 1 0 1 0 0 .01'],
  contract: ['M6 3h9l4 4v14H6z', 'M15 3v4h4', 'M9 12h7', 'M9 16h4'],
  receipt: ['M6 3h12v18l-3-2-3 2-3-2-3 2z', 'M9 8h6', 'M9 12h6'],
  sliders: [
    'M21 4h-7',
    'M10 4H3',
    'M21 12h-9',
    'M8 12H3',
    'M21 20h-5',
    'M12 20H3',
    'M14 2v4',
    'M8 10v4',
    'M16 18v4',
  ],
  clock: ['M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z', 'M12 7.5V12l3 2'],
  shield: ['M12 3l8 3v6c0 4.5-3.2 8-8 9.5C7.2 20 4 16.5 4 12V6z', 'M9 12l2.2 2.2L15.5 10'],
  pencil: ['M4 20h4L19 9l-4-4L4 16z', 'M13.5 6.5l4 4'],
  archive: ['M3 5h18v4H3z', 'M5 9v10a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V9', 'M10 13h4'],
  restore: ['M3.5 12a8.5 8.5 0 1 0 2.6-6.1L3.5 8.5', 'M3.5 3.5v5h5'],
  refresh: ['M20 12a8 8 0 1 1-2.3-5.7L20 8.5', 'M20 3.5v5h-5'],
  play: ['M7 4.5v15l12-7.5z'],
  pause: ['M8 5v14', 'M16 5v14'],
  calendar: [
    'M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z',
    'M4 10h16',
    'M8 3v4',
    'M16 3v4',
  ],
  construction: ['M3 20h18', 'M5 20V9l7-5 7 5v11', 'M9 20v-6h6v6'],
} as const satisfies Record<string, readonly string[]>;

export type IconName = keyof typeof ICON_PATHS;

/** A decorative inline icon. Pair it with visible text, or give the button around it an aria-label. */
@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[style.display]': '"inline-flex"', '[style.flex]': '"none"' },
  template: `
    <svg
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      @for (path of paths(); track $index) {
        <path [attr.d]="path" />
      }
    </svg>
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
  readonly size = input(20);

  protected readonly paths = computed<readonly string[]>(() => ICON_PATHS[this.name()]);
}
