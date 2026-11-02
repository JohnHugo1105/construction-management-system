import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { CollaborationApi } from './collaboration-api';
import { WORKER, comment, commentPage } from './collaboration.fixtures';
import { CommentPage, DiscussionComment } from './collaboration.models';
import { Discussion } from './discussion';

@Component({
  imports: [Discussion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-discussion kind="PurchaseOrder" targetId="po1" />`,
})
class Host {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const pia = { id: 'user-2', displayName: 'Pia Santos' };

async function open(page: CommentPage, api: Record<string, Mock<Call>> = {}) {
  const calls = {
    comments: stub(() => of(page)),
    colleagues: stub(() => of([pia, { id: 'user-1', displayName: 'Ana Reyes' }])),
    ...api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: CollaborationApi, useValue: calls },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['ProjectManager'],
    permissions: WORKER,
  });
  const fixture = TestBed.createComponent(Host);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, calls };
}

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

describe('Discussion', () => {
  it('reads the record discussion newest first, with mentions in bold and removed comments in place', async () => {
    const { root, calls } = await open(
      commentPage([
        comment({
          id: 'c2',
          text: 'Yes. @Ana Reyes will tell the site team.',
          mentions: [{ id: 'user-1', displayName: 'Ana Reyes' }],
          canRemove: true,
        }),
        comment({
          id: 'c1',
          text: null,
          isVoided: true,
          voidedByName: 'Pia Santos',
          voidedAtUtc: '2026-11-02T03:00:00Z',
        }),
      ]),
    );

    expect(calls['comments']).toHaveBeenCalledWith('PurchaseOrder', 'po1', 1, 20);
    expect(text(root, 'h2')).toBe('Discussion');
    expect(texts(root, '.thread__text')).toEqual(['Yes. @Ana Reyes will tell the site team.']);
    expect(text(root, '.thread__text strong')).toBe('@Ana Reyes');
    expect(text(root, '.thread__item--removed')).toContain('Removed by Pia Santos, 2 Nov 2026');
    expect(root.querySelectorAll('.thread__remove').length).toBe(1);
    expect(root.textContent).not.toContain('Show older comments');
  });

  it('says why the discussion is read-only and offers no comment box', async () => {
    const { root } = await open(
      commentPage([comment()], {
        canWrite: false,
        readOnlyReason: 'This project is archived, so its discussion is read-only.',
      }),
    );

    expect(root.querySelector('textarea')).toBeNull();
    expect(root.textContent).toContain('This project is archived, so its discussion is read-only.');
  });

  it('offers the people who can see the record after an @ and tells the one chosen', async () => {
    const posted = comment({
      id: 'c9',
      authorUserId: 'user-1',
      authorName: 'Ana Reyes',
      text: 'Please check @Pia Santos',
      mentions: [pia],
    });
    const addComment = stub(() => of(posted));
    const { fixture, root, calls } = await open(commentPage([]), { addComment });
    expect(root.textContent).toContain(
      'No comments yet. Ask a question or leave a note for the team.',
    );

    const box = root.querySelector<HTMLTextAreaElement>('textarea')!;
    type(root, 'textarea', 'Please check @pi');
    box.setSelectionRange(box.value.length, box.value.length);
    box.dispatchEvent(new Event('input', { bubbles: true }));
    await wait(250);
    await settle(fixture);

    expect(calls['colleagues']).toHaveBeenLastCalledWith('pi', {
      kind: 'PurchaseOrder',
      id: 'po1',
    });
    // The signed-in person is not offered: mentioning yourself tells nobody.
    expect(texts(root, '.suggestions__item')).toEqual(['Pia Santos']);

    buttonNamed(root, 'Pia Santos').click();
    await settle(fixture);
    expect(box.value).toBe('Please check @Pia Santos ');
    expect(root.textContent).toContain('Telling Pia Santos in the app.');

    buttonNamed(root, 'Post comment').click();
    await settle(fixture);

    expect(addComment).toHaveBeenCalledWith(
      {
        kind: 'PurchaseOrder',
        targetId: 'po1',
        text: 'Please check @Pia Santos',
        mentionUserIds: ['user-2'],
      },
      expect.any(String),
    );
    expect(texts(root, '.thread__text')).toEqual(['Please check @Pia Santos']);
    expect(box.value).toBe('');
  });

  it('asks for words before posting and shows a refusal in plain words', async () => {
    const refusal = new ApiError({
      status: 422,
      title: 'Check the comment',
      detail: 'Some details need attention.',
      code: 'validation_failed',
      errors: {
        mentionUserIds: ["Pia Santos can't see this record, so they can't be mentioned here."],
      },
    });
    const addComment = stub(() => throwError(() => refusal));
    const { fixture, root } = await open(commentPage([]), { addComment });

    buttonNamed(root, 'Post comment').click();
    await settle(fixture);
    expect(addComment).not.toHaveBeenCalled();
    expect(text(root, '.field__error')).toBe('Write your comment first.');

    type(root, 'textarea', 'Hello');
    buttonNamed(root, 'Post comment').click();
    await settle(fixture);
    expect(text(root, 'app-alert')).toContain("Pia Santos can't see this record");
  });

  it('removes a comment with a reason and shows who removed it', async () => {
    const mine = comment({
      id: 'c3',
      authorUserId: 'user-1',
      authorName: 'Ana Reyes',
      canRemove: true,
    });
    const removed: DiscussionComment = {
      ...mine,
      text: null,
      isVoided: true,
      voidedByName: 'Ana Reyes',
      voidedAtUtc: '2026-11-02T04:00:00Z',
      canRemove: false,
      version: 1,
    };
    const removeComment = stub(() => of(removed));
    const { fixture, root } = await open(commentPage([mine]), { removeComment });

    root.querySelector<HTMLButtonElement>('.thread__remove')!.click();
    await settle(fixture);
    type(root, 'dialog textarea', 'Posted on the wrong order');
    buttonNamed(root, 'Remove comment').click();
    await settle(fixture);

    expect(removeComment).toHaveBeenCalledWith('c3', 'Posted on the wrong order', 0);
    expect(text(root, '.thread__item--removed')).toContain('Removed by Ana Reyes');
  });

  it('brings older comments on request', async () => {
    const first = commentPage([comment({ id: 'c2' })], { total: 2 });
    const comments = stub((...args: unknown[]) =>
      of(
        args[2] === 1
          ? first
          : commentPage([comment({ id: 'c1', text: 'First one' })], { total: 2, page: 2 }),
      ),
    );
    const { fixture, root } = await open(first, { comments });

    buttonNamed(root, 'Show older comments').click();
    await settle(fixture);

    expect(comments).toHaveBeenLastCalledWith('PurchaseOrder', 'po1', 2, 20);
    expect(texts(root, '.thread__text')).toEqual(['Can the pour move to Monday?', 'First one']);
    expect(root.textContent).not.toContain('Show older comments');
  });
});
