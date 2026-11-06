namespace ConstructOPX.Domain.Common;

/// <summary>Sign-in failed. The message is safe to show: it never says whether the username or the password was wrong.</summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);
