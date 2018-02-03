using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Poc.Sqltabledependency.RefactoredVersion {
  public class DbTableListener<T> {
    public DbTableListener(string connectionString) {
      ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public string EventQueue { get; } = "EventQueue";

    public string SchemaName { get; } = "dbo";

    public event EventHandler<TableChangedEventArgs<T>> TableChanged;

    public Task Start(CancellationToken token) => 
      Task.Factory.StartNew(
        () => EventLoop(token),
        token,
        TaskCreationOptions.None,
        new SingleThreadTaskScheduler(ApartmentState.MTA)
      ).Unwrap();

    private async Task EventLoop(CancellationToken token) {
      while (!token.IsCancellationRequested) {
        token.ThrowIfCancellationRequested();
        using (var scope = new TransactionScope()) {
          var message = await ReceiveEvent(token);
          if (!string.IsNullOrWhiteSpace(message))
            OnTableChanged(message);
          scope.Complete();
        }
      }
    }

    private void OnTableChanged(string message) {
      var root = XElement.Parse(message.Trim((char)65279));

      var deleted = root.Descendants("deleted").FirstOrDefault();
      var inserted = root.Descendants("inserted").FirstOrDefault();
      var eventArgs = CreateEventArgs(
        FromXElement(deleted, "deleted"),
        FromXElement(inserted, "inserted")
      );

      if (eventArgs != null)
        TableChanged?.Invoke(this, eventArgs);
    }

    private static TableChangedEventArgs<T> CreateEventArgs(T deleted, T inserted) {
      if (deleted != null && inserted != null)
        return new RowUpdatedEventArgs<T> { Before = deleted, After = inserted };
      if (deleted != null)
        return new RowDeletedEventArgs<T> { DeletedRow = deleted };
      if (inserted != null)
        return new RowInsertedEventArgs<T> { InsertedRow = inserted };
      return null;
    }

    private static T FromXElement(XNode xElement, string root) {
      if (xElement == null) return default(T);
      var xmlSerializer = new XmlSerializer(typeof(T), new XmlRootAttribute(root));
      return (T)xmlSerializer.Deserialize(xElement.CreateReader());
    }


    private async Task<string> ReceiveEvent(CancellationToken token) {
      const int commandTimeout = 300;

      using (SqlConnection conn = new SqlConnection(ConnectionString))
      using (SqlCommand command = new SqlCommand(WaitCommand, conn)) {
        await conn.OpenAsync(token);
        command.CommandType = CommandType.Text;
        command.CommandTimeout = commandTimeout;
        using (var reader = await command.ExecuteReaderAsync(token)) {
          if (!await reader.ReadAsync(token))
            return string.Empty;
          if (await reader.IsDBNullAsync(0, token))
            return string.Empty;
          return await reader.GetFieldValueAsync<string>(0, token);
        }
      }
    }
    private string WaitCommand => $@"
                DECLARE @ConvHandle UNIQUEIDENTIFIER
                DECLARE @message VARBINARY(MAX)
                WAITFOR 
                (
                  RECEIVE TOP(1) 
                    @ConvHandle=Conversation_Handle,
                    @message=message_body 
                  FROM [{SchemaName}].[{EventQueue}]
                );
                --IF (@@ROWCOUNT != 0)
                BEGIN
                  END CONVERSATION @ConvHandle; 
                END
                SELECT CAST(@message AS NVARCHAR(MAX)) 
            ";

  }
}