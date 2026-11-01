using System.Text.Json;
using Cms.Domain.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;

namespace Cms.Api.Errors;

/// <summary>
/// The one place that turns exceptions into responses. Business and validation messages are already written for the end user and are
/// returned as is. Anything unexpected becomes a generic message plus a trace id; exception text, SQL and stack traces never leave the server.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationFailedException validation:
                await ApiProblems.WriteAsync(context, StatusCodes.Status422UnprocessableEntity, "Some details need your attention",
                    "Check the highlighted fields and try again.", "validation_failed", validation.Errors);
                return true;
            case ConcurrencyConflictException conflict:
                await ApiProblems.WriteAsync(context, StatusCodes.Status409Conflict, "This record changed", conflict.Message, "concurrency_conflict");
                return true;
            case DomainRuleException rule:
                await ApiProblems.WriteAsync(context, StatusCodes.Status409Conflict, "That can't be done right now", rule.Message, "rule_violation");
                return true;
            case NotFoundException missing:
                await ApiProblems.WriteAsync(context, StatusCodes.Status404NotFound, "Not found", missing.Message, "not_found");
                return true;
            case AuthenticationFailedException auth:
                await ApiProblems.WriteAsync(context, StatusCodes.Status401Unauthorized, "Couldn't sign you in", auth.Message, "sign_in_failed");
                return true;
            case AntiforgeryValidationException:
                await ApiProblems.WriteAsync(context, StatusCodes.Status400BadRequest, "Your session expired",
                    "Reload the page and try again.", "antiforgery");
                return true;
            case BadHttpRequestException or JsonException:
                await ApiProblems.WriteAsync(context, StatusCodes.Status400BadRequest, "That request couldn't be read",
                    "Something in the request isn't in the expected form. Reload the page and try again.", "bad_request");
                return true;
            case UnauthorizedAccessException:
                await ApiProblems.WriteAsync(context, StatusCodes.Status403Forbidden, "Not allowed", "You don't have access to do that.", "forbidden");
                return true;
            case OperationCanceledException when context.RequestAborted.IsCancellationRequested:
                return true;
            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                await ApiProblems.WriteAsync(context, StatusCodes.Status500InternalServerError, "Something went wrong",
                    "We couldn't finish that. Try again, and if it keeps happening, tell your administrator the reference below.", "server_error");
                return true;
        }
    }
}
