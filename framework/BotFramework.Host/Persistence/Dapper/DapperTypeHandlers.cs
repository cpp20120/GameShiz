using System.Data;
using Dapper;

namespace BotFramework.Host.Persistence;

internal static class DapperTypeHandlers
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1) return;
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
    }
}
