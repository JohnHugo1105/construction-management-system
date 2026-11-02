import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../../core/api/api-error';
import { settle, text, texts } from '../../../testing/dom';
import { CapabilitiesApi } from './capabilities-api';
import { Capabilities } from './capabilities.models';
import { SystemReadinessPage } from './system-readiness-page';

const READY: Capabilities = {
  items: [
    {
      key: 'file-storage',
      name: 'File storage',
      state: 'Failing',
      message: "File storage can't be written to right now, so files can't be uploaded.",
      fallback: 'Add a link to the document instead.',
    },
    {
      key: 'virus-scanning',
      name: 'Virus scanning',
      state: 'NeedsSetup',
      message: 'Files are stored and offered only as downloads; they are not scanned for viruses.',
      fallback: 'Open files only from people you know.',
    },
    {
      key: 'email-delivery',
      name: 'Email delivery',
      state: 'Available',
      message: 'Emails are sent.',
      fallback: null,
    },
  ],
};

async function open(list: () => ReturnType<CapabilitiesApi['list']>) {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), { provide: CapabilitiesApi, useValue: { list } }],
  });
  const fixture = TestBed.createComponent(SystemReadinessPage);
  await settle(fixture);
  return fixture.nativeElement as HTMLElement;
}

describe('SystemReadinessPage', () => {
  it('names each capability with its state in words, what it means and what still works', async () => {
    const root = await open(() => of(READY));

    expect(texts(root, '.row-card__title')).toEqual(['File storage', 'Virus scanning', 'Email delivery']);
    expect(texts(root, 'app-status-chip')).toEqual(['Not working', 'Not set up', 'Working']);
    const cards = root.querySelectorAll<HTMLElement>('.row-card');
    expect(text(cards[0]!, '.text-muted')).toBe('Meanwhile: Add a link to the document instead.');
    expect(cards[2]!.querySelector('.text-muted')).toBeNull();
  });

  it('shows a refusal in plain words with a way to try again', async () => {
    const root = await open(() =>
      throwError(
        () => new ApiError({ status: 0, title: "Can't reach the server", detail: 'Check your internet connection, then try again.' }),
      ),
    );

    expect(root.textContent).toContain("Can't reach the server");
    expect(root.querySelector('.row-card')).toBeNull();
  });
});
