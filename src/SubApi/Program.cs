using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("aspiredemo");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/database-info", async (
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken) =>
{
    await using var command = dataSource.CreateCommand(
        """
        SELECT
            current_database()
            || ' | PostgreSQL '
            || current_setting('server_version')
            || ' | '
            || clock_timestamp()::text
        """);

    return (string?)await command.ExecuteScalarAsync(cancellationToken)
        ?? throw new InvalidOperationException("PostgreSQL no devolvió información.");
});

app.Run();
