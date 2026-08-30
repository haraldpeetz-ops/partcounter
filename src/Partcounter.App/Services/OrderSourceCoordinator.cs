namespace Partcounter.Services;

public enum OrderSourceKind { ArburgAls, ProAlpha }

public static class OrderSourceCoordinator
{
    public const string ActiveSourceSettingKey = "OrderSource.Active";
    public const string AlsDisplayName = "ARBURG ALS";
    public const string ProAlphaDisplayName = "proALPHA";

    public static async Task<OrderSourceKind> GetActiveAsync(DatabaseService database)
    {
        var value = await database.GetSettingAsync(ActiveSourceSettingKey);
        return string.Equals(value, ProAlphaDisplayName, StringComparison.OrdinalIgnoreCase)
            ? OrderSourceKind.ProAlpha : OrderSourceKind.ArburgAls;
    }

    public static Task SetActiveAsync(DatabaseService database, OrderSourceKind source) =>
        database.SetSettingAsync(ActiveSourceSettingKey, source == OrderSourceKind.ProAlpha ? ProAlphaDisplayName : AlsDisplayName);

    public static async Task<bool> IsActiveAsync(DatabaseService database, OrderSourceKind source) =>
        await GetActiveAsync(database) == source;
}
