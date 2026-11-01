import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { ApiError, asApiError, toApiError } from './api-error';

function failure(
  status: number,
  body: unknown,
  headers: Record<string, string> = {},
): HttpErrorResponse {
  return new HttpErrorResponse({
    status,
    statusText: 'status',
    error: body,
    headers: new HttpHeaders(headers),
    url: '/api/x',
  });
}

describe('toApiError', () => {
  it('maps a 422 to field errors keyed by the request field name', () => {
    const error = toApiError(
      failure(422, {
        title: 'Some details need your attention',
        detail: 'Check the highlighted fields and try again.',
        code: 'validation_failed',
        traceId: 'abc123',
        errors: {
          name: ['Enter the project name.'],
          contractPriceMinor: ["The contract price can't be negative."],
        },
      }),
    );

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(422);
    expect(error.isValidation).toBe(true);
    expect(error.errors).toEqual({
      name: ['Enter the project name.'],
      contractPriceMinor: ["The contract price can't be negative."],
    });
    expect(error.firstError('name')).toBe('Enter the project name.');
    expect(error.firstError('startDate')).toBeNull();
    expect(error.detail).toBe('Check the highlighted fields and try again.');
    expect(error.traceId).toBe('abc123');
  });

  it('recognises a concurrency conflict and keeps the API wording', () => {
    const error = toApiError(
      failure(409, {
        title: 'This record changed',
        detail:
          'Someone else changed this record while you were editing. Reload it to see the latest version, then try again.',
        code: 'concurrency_conflict',
      }),
    );

    expect(error.isConflict).toBe(true);
    expect(error.isRuleViolation).toBe(false);
    expect(error.detail).toContain('Someone else changed this record');
  });

  it('tells a business rule apart from a conflict', () => {
    const error = toApiError(
      failure(409, {
        title: "That can't be done right now",
        detail: 'This project is archived. Restore it before making changes.',
        code: 'rule_violation',
      }),
    );

    expect(error.isRuleViolation).toBe(true);
    expect(error.isConflict).toBe(false);
  });

  it('never shows what the server said when it failed', () => {
    const error = toApiError(
      failure(
        500,
        {
          title: 'Microsoft.Data.SqlClient.SqlException',
          detail: 'Cannot insert duplicate key at ConstructOPX.Infrastructure.Projects line 42',
          code: 'server_error',
          traceId: 'body-id',
        },
        { 'X-Correlation-Id': 'header-id' },
      ),
    );

    expect(error.isServerFailure).toBe(true);
    expect(error.title).toBe('Something went wrong');
    expect(error.detail).toBe("We couldn't finish that. Try again in a moment.");
    expect(`${error.title} ${error.detail} ${error.message}`).not.toMatch(
      /Sql|duplicate|ConstructOPX\.Infrastructure|line 42/,
    );
    // The reference to quote comes from the response header, since the body of a failure is not trusted.
    expect(error.traceId).toBe('header-id');
  });

  it('also hides a proxy error page (502 with an HTML body)', () => {
    const error = toApiError(failure(502, '<html><body>Bad gateway nginx/1.25</body></html>'));

    expect(error.isServerFailure).toBe(true);
    expect(error.detail).not.toContain('nginx');
    expect(error.detail).not.toContain('<html>');
  });

  it('gives a calm message when the server cannot be reached at all', () => {
    const error = toApiError(failure(0, new ProgressEvent('error')));

    expect(error.isNetworkFailure).toBe(true);
    expect(error.isServerFailure).toBe(true);
    expect(error.title).toBe("Can't reach the server");
    expect(error.detail).toBe('Check your internet connection, then try again.');
  });

  it('uses plain wording for a bare 401 or 403 that has no problem body', () => {
    const unauthorized = toApiError(failure(401, null));
    const forbidden = toApiError(failure(403, 'Status Code: 403; Forbidden'));

    expect(unauthorized.isUnauthorized).toBe(true);
    expect(unauthorized.detail).toBe('Sign in again to continue.');
    expect(forbidden.isForbidden).toBe(true);
    expect(forbidden.detail).toBe("You don't have access to do that.");
    expect(forbidden.detail).not.toContain('Status Code');
  });

  it('reads the reference from the header when the body has none', () => {
    const error = toApiError(
      failure(
        404,
        {
          title: 'Not found',
          detail: "We couldn't find that project. It may have been removed.",
          code: 'not_found',
        },
        { 'X-Correlation-Id': 'req-9' },
      ),
    );

    expect(error.isNotFound).toBe(true);
    expect(error.traceId).toBe('req-9');
  });

  it('ignores malformed field errors instead of failing', () => {
    const error = toApiError(
      failure(422, {
        title: 't',
        detail: 'd',
        errors: { name: 'not a list', other: [1, 2], ok: ['Fine.'] },
      }),
    );

    expect(error.errors).toEqual({ ok: ['Fine.'] });
  });
});

describe('asApiError', () => {
  it('passes an ApiError through and turns anything else into the calm generic failure', () => {
    const own = new ApiError({ status: 409, title: 'a', detail: 'b' });

    expect(asApiError(own)).toBe(own);
    const generic = asApiError(new TypeError('x is not a function'));
    expect(generic.detail).toBe("We couldn't finish that. Try again in a moment.");
    expect(generic.message).not.toContain('x is not a function');
  });
});
