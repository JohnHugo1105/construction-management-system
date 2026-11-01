using Cms.Domain.Features.Identity;

namespace Cms.Application.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>Who is making the request. Services take the tenant and the person from here, never from the request body.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    Guid TenantId { get; }
    string UserName { get; }
    string DisplayName { get; }
    AccessRole Roles { get; }
    string? CorrelationId { get; }
}

/// <summary>Saves everything the use case changed in one transaction. Stale saves surface as a concurrency conflict.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Adds a line to the audit trail. The line is saved together with the change it describes, or not at all.</summary>
public interface IAuditTrail
{
    /// <summary>Records an action by the signed-in person in their organization.</summary>
    void Record(string action, string entityType, Guid entityId, string summary);

    /// <summary>Records an action when nobody is signed in yet (first-time setup, sign-in).</summary>
    void RecordAs(Guid tenantId, Guid? userId, string userName, string action, string entityType, Guid entityId, string summary);
}

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Checks a password against a stored hash in constant time. A null hash (no such account) is checked against a precomputed dummy
    /// hash, so an unknown username takes as long as a wrong password and the two can't be told apart by timing.
    /// </summary>
    bool Verify(string? passwordHash, string password);
}
