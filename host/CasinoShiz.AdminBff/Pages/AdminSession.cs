namespace CasinoShiz.AdminBff.Pages;

internal static class AdminSession
{
    private const string RoleKey = "admin.role";
    private const string ActorIdKey = "admin.actor-id";
    private const string ActorNameKey = "admin.actor-name";
    public static bool IsAuthenticated(this ISession session) => session.GetString(RoleKey) is not null;
    public static bool IsSuperAdmin(this ISession session) =>
        string.Equals(session.GetString(RoleKey), "SuperAdmin", StringComparison.Ordinal);
    public static long ActorId(this ISession session) =>
        long.TryParse(session.GetString(ActorIdKey), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0;
    public static string ActorName(this ISession session) => session.GetString(ActorNameKey) ?? "Admin";
    public static string ActorRole(this ISession session) => session.GetString(RoleKey) ?? "Admin";
    public static void SignIn(this ISession session, string role, long actorId, string actorName)
    {
        session.SetString(RoleKey, role);
        session.SetString(ActorIdKey, actorId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        session.SetString(ActorNameKey, actorName);
    }
    public static void SignOut(this ISession session) => session.Clear();
}
