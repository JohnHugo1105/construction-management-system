using ConstructOPX.Api.Errors;

namespace ConstructOPX.Api.Security;

/// <summary>While a person must choose a new password, the only thing they can do is choose it (or sign out).</summary>
public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.FindFirst(ClaimNames.MustChangePassword)?.Value == "true" &&
            context.Request.Path.StartsWithSegments("/api") &&
            !context.Request.Path.StartsWithSegments("/api/identity"))
        {
            await ApiProblems.WriteAsync(context, StatusCodes.Status403Forbidden, "Choose a new password first",
                "An administrator set a temporary password for you. Choose your own password to continue.", "password_change_required");
            return;
        }

        await next(context);
    }
}
