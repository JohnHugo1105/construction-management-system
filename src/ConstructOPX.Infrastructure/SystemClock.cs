using ConstructOPX.Application.Common;

namespace ConstructOPX.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
