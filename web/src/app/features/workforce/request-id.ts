/**
 * A fresh id for one form submission, so the API can recognise a repeated submission (a double click, a retry after a dropped
 * connection) and answer with the original result instead of doing the work twice. 36 characters, so it fits every limit the API
 * has for such ids (the shortest is 40).
 */
export function newRequestId(): string {
  const crypto = globalThis.crypto;
  if (crypto && typeof crypto.randomUUID === 'function') return crypto.randomUUID();

  // Older browsers on plain http: build a version 4 style id from random numbers.
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (symbol) => {
    const digit = Math.floor(Math.random() * 16);
    return (symbol === 'x' ? digit : (digit & 0x3) | 0x8).toString(16);
  });
}
