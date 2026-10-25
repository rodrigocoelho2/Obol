using System.Data;
using System.Text.Json;
using Dapper;

namespace Obol.WebApi.Extensions.Data;

/// <summary>
/// Base class of every SQL repository. Repositories only call stored procedures of Obol.Database:
/// no SQL text, no validation, no business rules. Lists of rows travel as one JSON parameter that the
/// procedure reads with OPENJSON, so a whole import is a single round trip.
/// </summary>
/// <param name="connectionFactory">Opens database connections.</param>
internal abstract class RepositoryBase(ISqlConnectionFactory connectionFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    /// <summary>Gets the connection factory.</summary>
    protected ISqlConnectionFactory ConnectionFactory { get; } = connectionFactory;

    /// <summary>
    /// Runs a stored procedure and maps every row.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="procedure">The procedure, e.g. <c>[dbo].[UserList]</c>.</param>
    /// <param name="parameters">The parameters (an anonymous object or <see cref="DynamicParameters"/>).</param>
    /// <returns>The rows.</returns>
    protected async Task<List<T>> QueryAsync<T>(string procedure, object? parameters = null)
    {
        await using var connection = await ConnectionFactory.OpenAsync();
        var rows = await connection.QueryAsync<T>(procedure, parameters, commandType: CommandType.StoredProcedure);
        return rows.AsList();
    }

    /// <summary>
    /// Runs a stored procedure that returns at most one row.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="procedure">The procedure.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The row, or null.</returns>
    protected async Task<T?> QuerySingleOrDefaultAsync<T>(string procedure, object? parameters = null)
    {
        await using var connection = await ConnectionFactory.OpenAsync();
        return await connection.QuerySingleOrDefaultAsync<T>(procedure, parameters, commandType: CommandType.StoredProcedure);
    }

    /// <summary>
    /// Runs a stored procedure that returns one value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="procedure">The procedure.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The value.</returns>
    protected async Task<T> ExecuteScalarAsync<T>(string procedure, object? parameters = null)
    {
        await using var connection = await ConnectionFactory.OpenAsync();
        return (await connection.ExecuteScalarAsync<T>(procedure, parameters, commandType: CommandType.StoredProcedure))!;
    }

    /// <summary>
    /// Runs a stored procedure that returns no rows.
    /// </summary>
    /// <param name="procedure">The procedure.</param>
    /// <param name="parameters">The parameters.</param>
    /// <returns>A task that completes when the procedure has run.</returns>
    protected async Task ExecuteAsync(string procedure, object? parameters = null)
    {
        await using var connection = await ConnectionFactory.OpenAsync();
        await connection.ExecuteAsync(procedure, parameters, commandType: CommandType.StoredProcedure);
    }

    /// <summary>
    /// A DATE parameter value (SQL Server DATE ⇄ <see cref="DateOnly"/> goes through <see cref="DateTime"/>).
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>Midnight of that date.</returns>
    protected static DateTime Date(DateOnly date) => date.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Serialises rows for an OPENJSON parameter.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="rows">The rows.</param>
    /// <returns>The JSON array.</returns>
    protected static string Json<T>(IEnumerable<T> rows) => JsonSerializer.Serialize(rows, JsonOptions);
}
