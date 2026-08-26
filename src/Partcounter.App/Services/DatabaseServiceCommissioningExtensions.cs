using Partcounter.Models;

namespace Partcounter.Services;

public static class DatabaseServiceCommissioningExtensions
{
    public static async Task<CommissioningProfile?> LoadCommissioningProfileAsync(this DatabaseService database, int machineNumber)
    {
        var service = await CreateAsync(database);
        return await service.LoadProfileAsync(machineNumber);
    }

    public static async Task UpsertCommissioningProfileAsync(this DatabaseService database, CommissioningProfile profile)
    {
        var service = await CreateAsync(database);
        await service.UpsertProfileAsync(profile);
    }

    public static async Task<IReadOnlyList<CommissioningCheckRecord>> LoadCommissioningChecksAsync(this DatabaseService database, int machineNumber)
    {
        var service = await CreateAsync(database);
        return await service.LoadChecksAsync(machineNumber);
    }

    public static async Task UpsertCommissioningCheckAsync(this DatabaseService database, CommissioningCheckRecord record)
    {
        var service = await CreateAsync(database);
        await service.UpsertCheckAsync(record);
    }

    private static async Task<CommissioningDatabaseService> CreateAsync(DatabaseService database)
    {
        var service = new CommissioningDatabaseService(database.DatabasePath);
        await service.InitializeAsync();
        return service;
    }
}
