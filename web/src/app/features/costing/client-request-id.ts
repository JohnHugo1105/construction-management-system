/** A new random identifier (a GUID). Uses the browser's own generator and falls back to random bytes where it is missing. */
export function newGuid(): string {
  const cryptoApi = globalThis.crypto;
  if (typeof cryptoApi?.randomUUID === 'function') return cryptoApi.randomUUID();

  const bytes = new Uint8Array(16);
  cryptoApi.getRandomValues(bytes);
  bytes[6] = (bytes[6]! & 0x0f) | 0x40;
  bytes[8] = (bytes[8]! & 0x3f) | 0x80;
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

/**
 * The idempotency key of one form session. The same value goes with every attempt to save, so a double-click or a retry after a
 * dropped connection can never record the line twice (the server returns the first result for a repeated key). Only a
 * successful save makes the next save a new request.
 */
export class ClientRequestId {
  private value: string;

  constructor(private readonly create: () => string = newGuid) {
    this.value = create();
  }

  /** The key to send with the next attempt. Unchanged by a failed save. */
  get current(): string {
    return this.value;
  }

  /** The save went through: whatever is saved next is a different request. */
  renew(): void {
    this.value = this.create();
  }
}
