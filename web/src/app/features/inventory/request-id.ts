/**
 * The idempotency key for one form session. It stays the same for every attempt (so a double-click or a retry after a lost
 * answer returns the first result instead of doing it twice) and is replaced only after the action worked.
 */
export class RequestId {
  private value = newRequestId();

  current(): string {
    return this.value;
  }

  /** Call after a success, so the next use of the same form is a new request. */
  renew(): void {
    this.value = newRequestId();
  }
}

export function newRequestId(): string {
  const crypto = globalThis.crypto;
  if (typeof crypto?.randomUUID === 'function') return crypto.randomUUID();

  // Older browsers on plain http: build a version-4 id from random bytes.
  const bytes = new Uint8Array(16);
  if (typeof crypto?.getRandomValues === 'function') crypto.getRandomValues(bytes);
  else
    for (let index = 0; index < bytes.length; index++)
      bytes[index] = Math.floor(Math.random() * 256);
  bytes[6] = (bytes[6]! & 0x0f) | 0x40;
  bytes[8] = (bytes[8]! & 0x3f) | 0x80;
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
