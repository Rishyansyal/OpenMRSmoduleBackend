using System.Data;
using Microsoft.Data.Sqlite;

namespace Infrastructure.Persistence;

public sealed class SqliteConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
