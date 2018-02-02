using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Poc.Sqltabledependency {
  public sealed partial class SqlDependencyEx : IDisposable {
    private static int _currentId = 0;

    private static int GetIdentity => Interlocked.Increment(ref _currentId);

    private CancellationTokenSource _threadSource;

    private QueueInitializer _queueInitializer;
    private Task notificationLoop;

    public string ConnectionString { get; private set; }

    public string DatabaseName { get; private set; }

    public string SchemaName { get; private set; }

    public int Identity { get; private set; }

    public event EventHandler<TableChangedEventArgs> TableChanged;

    public event EventHandler NotificationProcessStopped;

    public SqlDependencyEx(
                          string connectionString,
                          string databaseName,
                          string tableName,
                          string schemaName = "dbo",
                          NotificationTypes listenerType = NotificationTypes.Insert | NotificationTypes.Update | NotificationTypes.Delete,
                          bool receiveDetails = true) {
      ConnectionString = connectionString;
      DatabaseName = databaseName;
      SchemaName = schemaName;
      Identity = GetIdentity;
      _queueInitializer = new QueueInitializer(databaseName, Identity, tableName, schemaName, connectionString, receiveDetails, listenerType);
    }

    public void Start() {
      _threadSource = new CancellationTokenSource();

      _queueInitializer.InstallNotification();


      // Pass the token to the cancelable operation.
      notificationLoop = Task.Factory.StartNew(
        () => NotificationLoop(_threadSource.Token),
        _threadSource.Token,
        TaskCreationOptions.None,//we dont understand that anyways
        new SingleThreadTaskScheduler());
    }


    public void Dispose() {
      Stop();
    }

    private async Task NotificationLoop(CancellationToken token) {
      try {
        while (true) {
          token.ThrowIfCancellationRequested();
          var message = await ReceiveEvent(token);
          if (!string.IsNullOrWhiteSpace(message)) {
            OnTableChanged(message);
          }
        }
      }
      catch {
        // ignored
      }
      finally {
        OnNotificationProcessStopped();
      }
    }




    /// <summary>
    /// T-SQL script-template which helps to receive changed data in monitorable table.
    /// {0} - database name.
    /// {1} - conversation queue name.
    /// {2} - timeout.
    /// {3} - schema name.
    /// </summary>
    private async Task<string> ReceiveEvent(CancellationToken token) {
      const int commandTimeout = 60000;
      var commandText = $@"
                DECLARE @ConvHandle UNIQUEIDENTIFIER
                DECLARE @message VARBINARY(MAX)
                USE [{DatabaseName}]
                WAITFOR 
                (
                  RECEIVE TOP(1) 
                    @ConvHandle=Conversation_Handle,
                    @message=message_body 
                FROM {SchemaName}.[ListenerQueue_{Identity}]
                ), TIMEOUT {commandTimeout / 2};

                BEGIN
                  END CONVERSATION @ConvHandle; 
                END
                SELECT CAST(@message AS NVARCHAR(MAX)) 
            ";

      using (SqlConnection conn = new SqlConnection(ConnectionString))
      using (SqlCommand command = new SqlCommand(commandText, conn)) {
        command.CommandType = CommandType.Text;
        command.CommandTimeout = commandTimeout;

        await conn.OpenAsync(token);
        using (var reader = await command.ExecuteReaderAsync(token)) {
          if (!await reader.ReadAsync(token) || await reader.IsDBNullAsync(0, token))
            return string.Empty;

          return reader.GetString(0);
        }
      }
    }
    private void OnTableChanged(string message) {
      var evnt = TableChanged;
      if (evnt == null) return;

      evnt.Invoke(this, new TableChangedEventArgs(message));
    }

    private void OnNotificationProcessStopped() {
      var evnt = NotificationProcessStopped;
      if (evnt == null) return;

      evnt.BeginInvoke(this, EventArgs.Empty, null, null);
    }

    public void Stop() {
      _queueInitializer.UninstallNotification();

      if (_threadSource?.Token.IsCancellationRequested != true) {
        return;
      }

      if (!_threadSource.Token.CanBeCanceled) {
        return;
      }

      _threadSource.Cancel();
      _threadSource.Dispose();
      notificationLoop.Wait();
    }
  }
}
