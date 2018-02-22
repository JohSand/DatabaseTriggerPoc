using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Poc.Sqltabledependency.RefactoredVersion {
  public class ListenerInstaller {
    public ListenerInstaller(string targetTable, string databaseName) {
      TargetTable = targetTable;
      DatabaseName = databaseName;
      Notification = NotificationTypes.Update | NotificationTypes.Insert | NotificationTypes.Delete;
    }

    public string TriggerName { get; } = "tr_Listener";
    public string QueueName { get; } = "EventQueue";
    public string ServiceName { get; } = "ListenerService";
    public string SchemaName { get; } = "dbo";
    public string TargetTable { get; }
    public string DatabaseName { get; }
    public NotificationTypes Notification { get; }

    public void InstallListener(string connString) {
      ExecuteNonQuery(EnableBroker, connString);
      ExecuteNonQuery(CreateService, connString);
      ExecuteNonQuery(CreateTrigger, connString);
    }

    public void UninstallListener(string connString) {
      ExecuteNonQuery(Uninstall, connString);
    }

    private void ExecuteNonQuery(string cmdText, string connString) {
      using (SqlConnection conn = new SqlConnection(connString))
      using (SqlCommand command = new SqlCommand(cmdText, conn)) {
        conn.Open();
        conn.ChangeDatabase(DatabaseName);
        command.CommandType = CommandType.Text;
        command.ExecuteNonQuery();
      }
    }

    private string TriggerTypeByListenerType =>
      Notification == NotificationTypes.None
        ? "INSERT"
        : Enum.GetValues(typeof(NotificationTypes))
          .OfType<NotificationTypes>()
          .Where(t => t != NotificationTypes.None && Notification.HasFlag(t))
          .Select(t => t.ToString().ToUpperInvariant())
          .StringJoin(", ");


    private const string SqlPermissionsInfo =
      @"
      DECLARE @msg VARCHAR(MAX)
      DECLARE @crlf CHAR(1)
      SET @crlf = CHAR(10)
      SET @msg = 'Current user must have following permissions: '
      SET @msg = @msg + '[CREATE PROCEDURE, CREATE SERVICE, CREATE QUEUE, SUBSCRIBE QUERY NOTIFICATIONS, CONTROL, REFERENCES] '
      SET @msg = @msg + 'that are required to start query notifications. '
      SET @msg = @msg + 'Grant described permissions with following script: ' + @crlf
      SET @msg = @msg + 'GRANT CREATE PROCEDURE TO [<username>];' + @crlf
      SET @msg = @msg + 'GRANT CREATE SERVICE TO [<username>];' + @crlf
      SET @msg = @msg + 'GRANT CREATE QUEUE  TO [<username>];' + @crlf
      SET @msg = @msg + 'GRANT REFERENCES ON CONTRACT::[DEFAULT] TO [<username>];' + @crlf
      SET @msg = @msg + 'GRANT SUBSCRIBE QUERY NOTIFICATIONS TO [<username>];' + @crlf
      SET @msg = @msg + 'GRANT CONTROL ON SCHEMA::[<schemaname>] TO [<username>];'
                    
      PRINT @msg
                ";

    public string CreateTrigger =>
      $@"
      CREATE TRIGGER [{SchemaName}].{TriggerName} 
      ON [{SchemaName}].[{TargetTable}] 
      AFTER {TriggerTypeByListenerType} 
      AS   
	      DECLARE @message XML

	      SET @message = 
		      (SELECT 
		        (SELECT * FROM deleted FOR xml AUTO, type, elements),
		        (SELECT * FROM inserted FOR xml AUTO, type, elements)
	        FOR xml PATH)

          If (@message IS NOT NULL)  
          BEGIN 
              DECLARE @Handle UNIQUEIDENTIFIER;   
              BEGIN DIALOG CONVERSATION @Handle   
              FROM SERVICE [{ServiceName}]   
              TO SERVICE '{ServiceName}' 
              ON CONTRACT [EventContract]  
              WITH ENCRYPTION = OFF;   
              SEND ON CONVERSATION @Handle   
              MESSAGE TYPE [EventMessage](@message);
          END";


    public string EnableBroker =>
      $@"
      -- Setup Service Broker
      IF EXISTS (SELECT * 
                 FROM sys.databases 
                 WHERE name = '{DatabaseName}' AND 
                        (is_broker_enabled = 0 OR is_trustworthy_on = 0)) 
      BEGIN
          ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
          ALTER DATABASE [{DatabaseName}] SET ENABLE_BROKER; 
          ALTER DATABASE [{DatabaseName}] SET MULTI_USER WITH ROLLBACK IMMEDIATE
      END";


    public string CreateService =>
      $@"
      {SqlPermissionsInfo}
      CREATE MESSAGE TYPE [EventMessage] VALIDATION = WELL_FORMED_XML
      CREATE CONTRACT [EventContract] AUTHORIZATION {SchemaName} ([EventMessage] SENT BY INITIATOR);
      CREATE QUEUE [{SchemaName}].[{QueueName}] WITH STATUS=ON
      CREATE SERVICE {ServiceName} AUTHORIZATION {SchemaName} 
      ON QUEUE [{SchemaName}].[{QueueName}] ([EventContract]); ";

    private string Uninstall =>
      $@"                        
      -- Notification Trigger drop statement.
      IF OBJECT_ID ('{SchemaName}.{TriggerName}', 'TR') IS NOT NULL
          DROP TRIGGER {SchemaName}.[{TriggerName}];

      -- Empty Queue
      DECLARE @serviceId INT
      SELECT @serviceId = service_id FROM sys.services 
      WHERE sys.services.name = '{ServiceName}'

      DECLARE @ConvHandle uniqueidentifier
      DECLARE Conv CURSOR FOR
      SELECT CEP.conversation_handle FROM sys.conversation_endpoints CEP
      WHERE CEP.service_id = @serviceId AND ([state] != 'CD' OR [lifetime] > GETDATE() + 1)

      OPEN Conv;
      FETCH NEXT FROM Conv INTO @ConvHandle;
      WHILE (@@FETCH_STATUS = 0) BEGIN
    	  END CONVERSATION @ConvHandle WITH CLEANUP;
          FETCH NEXT FROM Conv INTO @ConvHandle;
      END
      CLOSE Conv;
      DEALLOCATE Conv;

      -- Droping service and queue.
      DROP SERVICE [{ServiceName}];
      IF OBJECT_ID ('{SchemaName}.{QueueName}', 'SQ') IS NOT NULL
	      DROP QUEUE {SchemaName}.[{QueueName}];
  ";
  }
}