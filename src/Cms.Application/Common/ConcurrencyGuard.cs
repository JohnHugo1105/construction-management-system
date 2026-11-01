using Cms.Domain.Common;

namespace Cms.Application.Common;

public static class ConcurrencyGuard
{
    /// <summary>
    /// Compares the version the person opened with the version stored now. The database token alone can't catch a stale form,
    /// because the record is re-read just before saving.
    /// </summary>
    public static void Ensure(int currentVersion, int submittedVersion)
    {
        if (currentVersion != submittedVersion) throw new ConcurrencyConflictException();
    }
}
