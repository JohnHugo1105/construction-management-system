import { ClientRequestId, newGuid } from './client-request-id';

describe('ClientRequestId', () => {
  it('keeps the same key until a save succeeds, so a retry can never record a line twice', () => {
    let next = 0;
    const id = new ClientRequestId(() => `key-${++next}`);

    expect(id.current).toBe('key-1');
    // A refused save, a dropped connection and a second tap all send the same key.
    expect(id.current).toBe('key-1');
    expect(id.current).toBe('key-1');

    id.renew();
    expect(id.current).toBe('key-2');
  });

  it('starts every form session with a fresh key', () => {
    expect(new ClientRequestId().current).not.toBe(new ClientRequestId().current);
  });
});

describe('newGuid', () => {
  it('looks like a GUID the API accepts and is different each time', () => {
    const first = newGuid();
    expect(first).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(newGuid()).not.toBe(first);
  });

  it('falls back to random bytes where the browser has no randomUUID', () => {
    const original = globalThis.crypto.randomUUID;
    Object.defineProperty(globalThis.crypto, 'randomUUID', {
      value: undefined,
      configurable: true,
    });
    try {
      expect(newGuid()).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
      );
    } finally {
      Object.defineProperty(globalThis.crypto, 'randomUUID', {
        value: original,
        configurable: true,
      });
    }
  });
});
