using Microsoft.Data.Sqlite;

namespace Partcounter.Services;

/// <summary>
/// Process-wide SQLite writer serialization. WAL permits concurrent readers, but SQLite still
/// permits only one writer. All production-relevant writers use this gate in R001.25.
/// </summary>
public static class SqliteWriteCoordinator
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public const int BusyTimeoutSeconds = 15;

    public static string BuildConnectionString(string databasePath) =>
        $"Data Source={databasePath};Cache=Shared;Default Timeout={BusyTimeoutSeconds};Pooling=True";

    public static async Task ExecuteAsync(
        string databasePath,
        Func<SqliteConnection, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(action);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
            await connection.OpenAsync(cancellationToken);
            await ConfigureAsync(connection, cancellationToken);
            await action(connection);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<T> ExecuteAsync<T>(
        string databasePath,
        Func<SqliteConnection, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(action);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqliteConnection(BuildConnectionString(databasePath));
            await connection.OpenAsync(cancellationToken);
            await ConfigureAsync(connection, cancellationToken);
            return await action(connection);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task ConfigureAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout={BusyTimeoutSeconds * 1000}; PRAGMA foreign_keys=ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
