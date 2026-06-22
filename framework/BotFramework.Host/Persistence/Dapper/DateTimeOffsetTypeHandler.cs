using System.Data;
using Dapper;

namespace BotFramework.Host.Persistence;

internal sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        => parameter.Value = value.UtcDateTime;

    public override DateTimeOffset Parse(object value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
        string s => DateTimeOffset.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new InvalidCastException($"Cannot convert {value.GetType()} to DateTimeOffset"),
    };
}
