import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { buttonNamed, settle } from '../../testing/dom';
import { WorkerStatutoryDialog, statutoryWords } from './worker-statutory';
import { WorkforceApi } from './workforce-api';
import { workerWithPay } from './workforce.fixtures';

type Call = (...args: unknown[]) => Observable<unknown>;

describe('worker contributions', () => {
  it('words which contributions a worker pays', () => {
    expect(
      statutoryWords({
        sssMember: true,
        philHealthMember: true,
        pagIbigMember: true,
        taxExempt: false,
      }),
    ).toBe('SSS, PhilHealth and Pag-IBIG; tax withheld');
    expect(
      statutoryWords({
        sssMember: false,
        philHealthMember: true,
        pagIbigMember: false,
        taxExempt: true,
      }),
    ).toBe('PhilHealth; exempt from tax');
    expect(
      statutoryWords({
        sssMember: false,
        philHealthMember: false,
        pagIbigMember: false,
        taxExempt: false,
      }),
    ).toBe('No contributions; tax withheld');
  });

  it('opens with the worker’s choices and sends the changed ones with the worker’s version', async () => {
    const setStatutory: Mock<Call> = vi.fn<Call>(() => of(workerWithPay({ version: 5 })));
    TestBed.configureTestingModule({
      providers: [{ provide: WorkforceApi, useValue: { setStatutory } }],
    });
    const fixture = TestBed.createComponent(WorkerStatutoryDialog);
    fixture.componentRef.setInput(
      'worker',
      workerWithPay({
        statutory: {
          sssMember: true,
          philHealthMember: true,
          pagIbigMember: false,
          taxExempt: false,
        },
      }),
    );
    await settle(fixture);
    const root = fixture.nativeElement as HTMLElement;
    const boxes = root.querySelectorAll<HTMLInputElement>('input[type="checkbox"]');
    expect(Array.from(boxes).map((box) => box.checked)).toEqual([true, true, false, false]);

    boxes[0]!.click();
    boxes[3]!.click();
    buttonNamed(root, 'Save contributions').click();
    await settle(fixture);

    expect(setStatutory).toHaveBeenCalledWith(
      'w1',
      { sssMember: false, philHealthMember: true, pagIbigMember: false, taxExempt: true },
      4,
    );
  });
});
