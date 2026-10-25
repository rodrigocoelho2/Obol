using Microsoft.Data.SqlClient;

namespace Obol.WebApi.Extensions.Data;

/// <summary>
/// Opens connections to the Obol SQL Server database.
/// </summary>
internal interface ISqlConnectionFactory
{
    /// <summary>
    /// Opens a new connection. The caller disposes it.
    /// </summary>
    /// <returns>An open connection.</returns>
    Task<SqlConnection> OpenAsync();
}

/// <summary>
/// Connections from the "Obol" connection string (env <c>ConnectionStrings__Obol</c>).
/// </summary>
internal sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    /// <summary>The name of the connection string.</summary>
    public const string ConnectionStringName = "Obol";

    private readonly string _connectionString;

    /// <summary>
    /// Reads the connection string.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public SqlConnectionFactory(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConnectionStringName} is empty (env ConnectionStrings__{ConnectionStringName}). Set it, or turn on Obol:DemoMode to run on sample data.");
        }

        _connectionString = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = "Obol",
        }.ConnectionString;
    }

    /// <inheritdoc />
    public async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
