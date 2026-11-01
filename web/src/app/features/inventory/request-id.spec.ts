import { RequestId, newRequestId } from './request-id';

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

describe('RequestId', () => {
  it('is a GUID, the same for every attempt of one form session', () => {
    const id = new RequestId();

    const first = id.current();
    expect(first).toMatch(GUID);
    expect(id.current()).toBe(first);
    expect(id.current()).toBe(first);
  });

  it('is replaced only when renewed after a success', () => {
    const id = new RequestId();
    const first = id.current();

    id.renew();

    expect(id.current()).not.toBe(first);
    expect(id.current()).toMatch(GUID);
  });

  it('gives each form session its own id', () => {
    expect(new RequestId().current()).not.toBe(new RequestId().current());
  });
});

describe('newRequestId without crypto.randomUUID (plain http on an older browser)', () => {
  it('still builds a version 4 GUID from random bytes', () => {
    const original = globalThis.crypto;
    vi.stubGlobal('crypto', {
      getRandomValues: (bytes: Uint8Array) => {
        bytes.forEach((_, index) => (bytes[index] = (index * 37 + 11) % 256));
        return bytes;
      },
    });
    try {
      expect(newRequestId()).toMatch(GUID);
    } finally {
      vi.stubGlobal('crypto', original);
    }
  });
});
