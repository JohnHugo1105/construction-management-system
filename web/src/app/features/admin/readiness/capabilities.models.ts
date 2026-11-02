/** What `GET /api/capabilities` returns (see docs/api/documents.md): each thing the system depends on that may not be set up yet. */

export type CapabilityState = 'Available' | 'NeedsSetup' | 'Unavailable' | 'Failing';

export interface Capability {
  /** "file-storage", "virus-scanning", "email-delivery". */
  readonly key: string;
  /** "File storage". */
  readonly name: string;
  readonly state: CapabilityState;
  /** What the state means for people, in plain words. */
  readonly message: string;
  /** What people can do meanwhile; null when it is available. */
  readonly fallback: string | null;
}

export interface Capabilities {
  readonly items: readonly Capability[];
}
