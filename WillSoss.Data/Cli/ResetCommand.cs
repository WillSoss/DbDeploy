﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace WillSoss.Data.Cli
{
    internal class ResetCommand : CliCommand
    {
        private DatabaseBuilder _builder;
        private readonly string? _connectionString;
        private readonly ILogger _logger;

        public ResetCommand(DatabaseBuilder builder, string? connectionString, ILogger<ResetCommand> logger)
        {
            _builder = builder;
            _connectionString = connectionString;
            _logger = logger;
        }

        internal override async Task RunAsync(CancellationToken cancel)
        {
            if (!string.IsNullOrWhiteSpace(_connectionString))
                _builder = _builder.WithConnectionString(_connectionString);

            if (string.IsNullOrWhiteSpace(_builder.ConnectionString))
            {
                _logger.LogError("Connection string is required. Configure the connection string in the app or use --connectionstring <connectionstring>.");
                return;
            }

            var db = _builder.Build();

            _logger.LogInformation("Resetting database {0} on {1}.", db.GetDatabaseName(), db.GetServerName());

            await db.Reset();

            _logger.LogInformation("Reset complete for database {0} on {1}.", db.GetDatabaseName(), db.GetServerName());
        }

        internal static Command Create(IServiceCollection services)
        {
            var command = new Command("reset", "Runs the reset script on the database. Can be used to clean up data after test runs."); ;

            command.AddOption(ConnectionStringOption);

            command.SetHandler((cs) => services.AddTransient<CliCommand>(s => new ResetCommand(
                s.GetRequiredService<DatabaseBuilder>(),
                cs,
                s.GetRequiredService<ILogger<ResetCommand>>()
                )), ConnectionStringOption);

            return command;
        }
    }
}
