import { HttpErrorResponse } from '@angular/common/http';

/** The codes the API puts on its problem responses (see src/Cms.Api/Errors). */
export const ApiErrorCode = {
  validationFailed: 'validation_failed',
  concurrencyConflict: 'concurrency_conflict',
  ruleViolation: 'rule_violation',
  notFound: 'not_found',
  signInFailed: 'sign_in_failed',
  antiforgery: 'antiforgery',
  badRequest: 'bad_request',
  forbidden: 'forbidden',
  passwordChangeRequired: 'password_change_required',
  rateLimited: 'rate_limited',
  serverError: 'server_error',
} as const;

export interface ApiErrorInit {
  readonly status: number;
  readonly title: string;
  readonly detail: string;
  readonly code?: string | null;
  readonly errors?: Record<string, string[]>;
  readonly traceId?: string | null;
}

/**
 * What the screens see when the API refuses or fails. `title` and `detail` are always safe to show a person: for
 * business and validation problems they are the API's own end-user wording, and for server failures they are a calm
 * generic message (the server's text is never shown). `errors` is keyed by camelCase request field name.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail: string;
  readonly code: string | null;
  readonly errors: Record<string, string[]>;
  readonly traceId: string | null;

  constructor(init: ApiErrorInit) {
    super(init.detail || init.title);
    this.name = 'ApiError';
    this.status = init.status;
    this.title = init.title;
    this.detail = init.detail;
    this.code = init.code ?? null;
    this.errors = init.errors ?? {};
    this.traceId = init.traceId ?? null;
  }

  /** The record changed since the person opened it: ask them to reload. */
  get isConflict(): boolean {
    return this.status === 409 && this.code === ApiErrorCode.concurrencyConflict;
  }

  /** A business rule forbids this right now (409 with a plain-words message). */
  get isRuleViolation(): boolean {
    return this.status === 409 && this.code === ApiErrorCode.ruleViolation;
  }

  get isValidation(): boolean {
    return this.status === 422 || Object.keys(this.errors).length > 0;
  }

  get isNotFound(): boolean {
    return this.status === 404;
  }

  get isForbidden(): boolean {
    return this.status === 403;
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }

  /** The server failed, or could not be reached at all: show the calm message plus the reference. */
  get isServerFailure(): boolean {
    return this.status === 0 || this.status >= 500;
  }

  get isNetworkFailure(): boolean {
    return this.status === 0;
  }

  /** The first message for a field, if the server reported one. */
  firstError(field: string): string | null {
    return this.errors[field]?.[0] ?? null;
  }
}

export function isApiError(value: unknown): value is ApiError {
  return value instanceof ApiError;
}

/** What a catch block holds, as an ApiError: anything that is not one becomes the calm generic failure. */
export function asApiError(value: unknown): ApiError {
  if (value instanceof ApiError) return value;
  return new ApiError({
    status: 0,
    title: 'Something went wrong',
    detail: "We couldn't finish that. Try again in a moment.",
  });
}

interface ProblemBody {
  readonly title?: unknown;
  readonly detail?: unknown;
  readonly code?: unknown;
  readonly traceId?: unknown;
  readonly errors?: unknown;
}

const SERVER_FAILURE_TITLE = 'Something went wrong';
const SERVER_FAILURE_DETAIL = "We couldn't finish that. Try again in a moment.";
const NETWORK_FAILURE_TITLE = "Can't reach the server";
const NETWORK_FAILURE_DETAIL = 'Check your internet connection, then try again.';

/** Turns an HTTP failure into an ApiError. Server text is trusted only for problem responses below 500. */
export function toApiError(response: HttpErrorResponse): ApiError {
  const traceId = readTraceId(response);

  if (response.status === 0) {
    return new ApiError({
      status: 0,
      title: NETWORK_FAILURE_TITLE,
      detail: NETWORK_FAILURE_DETAIL,
      traceId,
    });
  }

  if (response.status >= 500) {
    // Never show what the server said on a failure: it may be a stack trace, SQL, or a proxy page.
    return new ApiError({
      status: response.status,
      title: SERVER_FAILURE_TITLE,
      detail: SERVER_FAILURE_DETAIL,
      code: ApiErrorCode.serverError,
      traceId,
    });
  }

  const body = readProblem(response.error);
  if (body === null)
    return new ApiError({ ...fallbackFor(response.status), status: response.status, traceId });

  const title = asText(body.title);
  const detail = asText(body.detail);
  const fallback = fallbackFor(response.status);
  return new ApiError({
    status: response.status,
    title: title || fallback.title,
    detail: detail || fallback.detail,
    code: asText(body.code) || null,
    errors: readFieldErrors(body.errors),
    traceId: asText(body.traceId) || traceId,
  });
}

/** Wording for a failure the API gave no usable problem body for (a plain 401 or 403, a proxy page). */
function fallbackFor(status: number): { title: string; detail: string; code: string | null } {
  switch (status) {
    case 401:
      return { title: 'Your session has ended', detail: 'Sign in again to continue.', code: null };
    case 403:
      return {
        title: 'Not allowed',
        detail: "You don't have access to do that.",
        code: ApiErrorCode.forbidden,
      };
    case 404:
      return {
        title: 'Not found',
        detail: "We couldn't find that. It may have been removed.",
        code: ApiErrorCode.notFound,
      };
    case 429:
      return {
        title: 'Too many attempts',
        detail: 'Wait a minute, then try again.',
        code: ApiErrorCode.rateLimited,
      };
    default:
      return { title: "That didn't work", detail: 'Reload the page and try again.', code: null };
  }
}

function readProblem(error: unknown): ProblemBody | null {
  if (error === null || typeof error !== 'object' || Array.isArray(error)) return null;
  return error as ProblemBody;
}

function readFieldErrors(value: unknown): Record<string, string[]> {
  const result: Record<string, string[]> = {};
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return result;
  for (const [field, messages] of Object.entries(value)) {
    const list = Array.isArray(messages)
      ? messages.filter((item): item is string => typeof item === 'string')
      : [];
    if (list.length > 0) result[field] = list;
  }
  return result;
}

function readTraceId(response: HttpErrorResponse): string | null {
  return response.headers?.get('X-Correlation-Id') ?? null;
}

function asText(value: unknown): string {
  return typeof value === 'string' ? value : '';
}
