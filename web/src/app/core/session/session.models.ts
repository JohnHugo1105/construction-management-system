/** What `GET /api/identity/session` returns: who is signed in, what they may do, and their company's settings. */
export interface Session {
  readonly userId: string;
  readonly tenantId: string;
  readonly username: string;
  readonly displayName: string;
  /** Role names as the API writes them ("ProjectManager"); use roleLabel() to show them. */
  readonly roles: readonly string[];
  /** Permission names such as "projects.manage" (see permissions.ts). */
  readonly permissions: readonly string[];
  readonly mustChangePassword: boolean;
  readonly organizationName: string;
  readonly currencyCode: string;
  readonly timeZoneId: string;
  /** The company VAT rate in basis points: 1,200 is 12%. */
  readonly vatRateBps: number;
}

export interface SetupStatus {
  readonly needsSetup: boolean;
}

export interface SignInRequest {
  readonly username: string;
  readonly password: string;
}

export interface BootstrapRequest {
  readonly companyName: string;
  readonly displayName: string;
  readonly username: string;
  readonly password: string;
}

export interface ChangePasswordRequest {
  readonly currentPassword: string;
  readonly newPassword: string;
}
