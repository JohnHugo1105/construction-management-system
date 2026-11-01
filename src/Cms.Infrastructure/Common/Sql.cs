namespace Cms.Infrastructure.Common;

public static class Sql
{
    /// <summary>The escape character passed as the third argument of <c>EF.Functions.Like</c>.</summary>
    public const string LikeEscape = "\\";

    /// <summary>
    /// Builds a "contains" pattern for LIKE with the wildcard characters in the person's text taken literally, so a search for 50%
    /// finds "50%" and not everything. (The desktop app escaped only the apostrophe.)
    /// </summary>
    public static string Contains(string text)
    {
        var escaped = text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
        return $"%{escaped}%";
    }
}
