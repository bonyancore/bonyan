using System.Data;
using System.Data.Common;

namespace Construction.Messaging;

/// <summary>
/// ADO.NET implementation of IProcessedEventStore executing against platform.processed_events.
/// Uses "INSERT INTO platform.processed_events (consumer, event_id, processed_at) VALUES (@consumer, @event_id, now()) ON CONFLICT DO NOTHING"
/// </summary>
public sealed class DbProcessedEventStore : IProcessedEventStore
{
    private readonly Func<CancellationToken, Task<DbConnection>> _connectionFactory;
    private readonly string _tableName;

    public DbProcessedEventStore(
        Func<CancellationToken, Task<DbConnection>> connectionFactory,
        string tableName = "platform.processed_events")
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _tableName = tableName;
    }

    public async Task<IConsumerTransactionContext> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new DbConsumerTransactionContext(connection, transaction, _tableName);
    }

    private sealed class DbConsumerTransactionContext : IConsumerTransactionContext
    {
        private readonly DbConnection _connection;
        private readonly DbTransaction _transaction;
        private readonly string _tableName;
        private bool _completed;

        public DbConsumerTransactionContext(DbConnection connection, DbTransaction transaction, string tableName)
        {
            _connection = connection;
            _transaction = transaction;
            _tableName = tableName;
        }

        public DbTransaction DbTransaction => _transaction;
        public DbConnection DbConnection => _connection;

        public async Task<bool> TryRecordProcessedAsync(
            string consumer,
            Guid eventId,
            CancellationToken cancellationToken = default)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.Transaction = _transaction;
            cmd.CommandText = $@"
                INSERT INTO {_tableName} (consumer, event_id, processed_at)
                VALUES (@consumer, @event_id, now())
                ON CONFLICT (consumer, event_id) DO NOTHING;";

            var pConsumer = cmd.CreateParameter();
            pConsumer.ParameterName = "consumer";
            pConsumer.Value = consumer;
            cmd.Parameters.Add(pConsumer);

            var pEventId = cmd.CreateParameter();
            pEventId.ParameterName = "event_id";
            pEventId.Value = eventId;
            cmd.Parameters.Add(pEventId);

            int rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows > 0;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (!_completed)
            {
                await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _completed = true;
            }
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (!_completed)
            {
                await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _completed = true;
            }
        }

        public void Dispose()
        {
            if (!_completed)
            {
                try { _transaction.Rollback(); } catch { /* ignore on dispose */ }
                _completed = true;
            }
            _transaction.Dispose();
            _connection.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed)
            {
                try { await _transaction.RollbackAsync().ConfigureAwait(false); } catch { /* ignore on dispose */ }
                _completed = true;
            }
            await _transaction.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
