using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cms.Api.Tests.Infrastructure;

/// <summary>
/// Counts the SQL commands EF runs. Use <see cref="Measure"/> around one request to learn how many statements it cost, then assert the
/// number is the same for a small and a large data set.
/// </summary>
public sealed class SqlStatementCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public async Task<int> MeasureAsync(Func<Task> action)
    {
        var before = Count;
        await action();
        return Count - before;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}
