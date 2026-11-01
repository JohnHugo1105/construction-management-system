using Cms.Application.Common;
using Cms.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Persistence;

/// <summary>Saves in one transaction and turns database failures into messages a person can act on, never SQL text.</summary>
public sealed class UnitOfWork(CmsDbContext db) : IUnitOfWork
{
    private const int DuplicateKey = 2601;
    private const int UniqueConstraint = 2627;

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: DuplicateKey or UniqueConstraint })
        {
            throw new DomainRuleException("That already exists. Check for a duplicate, or reload the page and try again.");
        }
    }
}
