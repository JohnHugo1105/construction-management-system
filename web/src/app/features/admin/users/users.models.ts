/** A person who can sign in, as the API returns them. Roles are the API's role names ("ProjectManager"). */
export interface UserAccount {
  readonly id: string;
  readonly username: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly roles: readonly string[];
  /** False when an administrator turned the account off. */
  readonly isActive: boolean;
  /** True while the person still has a temporary password. */
  readonly mustChangePassword: boolean;
  readonly lastSignInUtc: string | null;
  readonly lockedUntilUtc: string | null;
  readonly version: number;
}

export interface CreateUserRequest {
  readonly username: string;
  readonly displayName: string;
  readonly email: string | null;
  readonly password: string;
  readonly roles: readonly string[];
}

export interface UpdateUserRequest {
  readonly displayName: string;
  readonly email: string | null;
  readonly roles: readonly string[];
  readonly version: number;
}
