using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Poc.Sqltabledependency {
  public sealed partial class SqlDependencyEx {
    public class QueueInitializer {
      public QueueInitializer(string databaseName, int identity, string tableName, string schemaName, string connectionString, bool detailsIncluded, NotificationTypes notificationTypes) {
        DatabaseName = databaseName;
        Identity = identity;
        TableName = tableName;
        SchemaName = schemaName;
        ConnectionString = connectionString;
        DetailsIncluded = detailsIncluded;
        NotificationTypes = notificationTypes;
      }

      public string DatabaseName { get; private set; }

      public int Identity { get; private set; }

      public string TableName { get; private set; }

      public string SchemaName { get; private set; }

      public string ConnectionString { get; private set; }

      public bool DetailsIncluded { get; private set; }

      public NotificationTypes NotificationTypes { get; private set; }

      private void ExecuteNonQuery(string commandText) {
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        using (SqlCommand command = new SqlCommand(commandText, conn)) {
          conn.Open();
          command.CommandType = CommandType.Text;
          var result = command.ExecuteNonQuery();
        }
      }


      public void InstallNotification() {
        ExecuteNonQuery(SqlFormatCreateInstallationProcedure);

        ExecuteNonQuery(GetSqlFormatExecuteProcedure(InstallListenerProcedureName));
      }

      public string GetUninstallNotificationProcedureScript2() => SqlFormatCreateUninstallationProcedure;

      public void UninstallNotification() {
        ExecuteNonQuery(GetUninstallNotificationProcedureScript2());
        ExecuteNonQuery(GetSqlFormatExecuteProcedure(UninstallListenerProcedureName));
      }

      private string TriggerTypeByListenerType => 
        NotificationTypes == NotificationTypes.None
          ? "INSERT"
          : Enum.GetValues(typeof(NotificationTypes)).OfType<NotificationTypes>()
            .Where(t => t != NotificationTypes.None && NotificationTypes.HasFlag(t))
            .Select(t => t.ToString().ToUpperInvariant())
            .StringJoin(", ");

      private string InstallListenerProcedureName => $"sp_InstallListenerNotification_{Identity}";

      private string UninstallListenerProcedureName => $"sp_UninstallListenerNotification_{Identity}";

      private string ConversationQueueName => $"ListenerQueue_{Identity}";

      private string ConversationServiceName => $"ListenerService_{Identity}";

      private string ConversationTriggerName => $"tr_Listener_{Identity}";

      /// <summary>
      /// T-SQL script-template which executes stored procedure.
      /// {0} - database name.
      /// {1} - procedure name.
      /// {2} - schema name.
      /// </summary>
      private string GetSqlFormatExecuteProcedure(string procName) {
        return $@"
                USE [{DatabaseName}]
                IF OBJECT_ID ('{SchemaName}.{procName}', 'P') IS NOT NULL
                    EXEC {SchemaName}.{procName}
            ";
      }
      #region Procedures

      private const string SqlPermissionsInfo = @"
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

            /// <summary>
      /// T-SQL script-template which creates notification setup procedure.
      /// </summary>
      private string SqlFormatCreateInstallationProcedure => $@"
                USE [{DatabaseName}]
                {SqlPermissionsInfo}
                IF OBJECT_ID ('{SchemaName}.{InstallListenerProcedureName}', 'P') IS NULL
                BEGIN
                    EXEC ('
                        CREATE PROCEDURE {SchemaName}.{InstallListenerProcedureName}
                        AS
                        BEGIN
                            -- Service Broker configuration statement.
                            {SqlFormatInstallSeviceBrokerNotification}

                            -- Notification Trigger check statement.
                            {SqlFormatCheckNotificationTrigger}

                            -- Notification Trigger configuration statement.
                            DECLARE @triggerStatement NVARCHAR(MAX)
                            DECLARE @select NVARCHAR(MAX)
                            DECLARE @sqlInserted NVARCHAR(MAX)
                            DECLARE @sqlDeleted NVARCHAR(MAX)
                            
                            SET @triggerStatement = N''{SqlFormatCreateNotificationTrigger}''
                            
                            SET @select = STUFF((SELECT '','' + ''['' + COLUMN_NAME + '']''
						                         FROM INFORMATION_SCHEMA.COLUMNS
						                         WHERE DATA_TYPE NOT IN  (''text'',''ntext'',''image'',''geometry'',''geography'') AND TABLE_SCHEMA = ''{SchemaName}'' AND TABLE_NAME = ''{TableName}'' AND TABLE_CATALOG = ''{DatabaseName}''
						                         FOR XML PATH ('''')
						                         ), 1, 1, '''')
                            SET @sqlInserted = 
                                N''SET @retvalOUT = (SELECT '' + @select + N'' 
                                                     FROM INSERTED 
                                                     FOR XML PATH(''''row''''), ROOT (''''inserted''''))''
                            SET @sqlDeleted = 
                                N''SET @retvalOUT = (SELECT '' + @select + N'' 
                                                     FROM DELETED 
                                                     FOR XML PATH(''''row''''), ROOT (''''deleted''''))''                            
                            SET @triggerStatement = REPLACE(@triggerStatement
                                                     , ''%inserted_select_statement%'', @sqlInserted)
                            SET @triggerStatement = REPLACE(@triggerStatement
                                                     , ''%deleted_select_statement%'', @sqlDeleted)

                            EXEC sp_executesql @triggerStatement
                        END
                        ')
                END
            ";

      /// <summary>
      /// T-SQL script-template which creates notification uninstall procedure.
      /// </summary>
      private string SqlFormatCreateUninstallationProcedure => $@"
                USE [{DatabaseName}]
                { SqlPermissionsInfo }
                IF OBJECT_ID ('{SchemaName}.{UninstallListenerProcedureName}', 'P') IS NULL
                BEGIN
                    EXEC ('
                        CREATE PROCEDURE {SchemaName}.{UninstallListenerProcedureName}
                        AS
                        BEGIN
                            -- Notification Trigger drop statement.
                            {SqlFormatDeleteNotificationTrigger}

                            -- Service Broker uninstall statement.
                            {SqlFormatUninstallServiceBrokerNotification}

                            IF OBJECT_ID (''{SchemaName}.{InstallListenerProcedureName}'', ''P'') IS NOT NULL
                                DROP PROCEDURE {SchemaName}.{InstallListenerProcedureName}
                            
                            DROP PROCEDURE {SchemaName}.{UninstallListenerProcedureName}
                        END
                        ')
                END
            ";

      #endregion
      #region ServiceBroker notification
      /// <summary>
      /// T-SQL script-template which prepares database for ServiceBroker notification.
      /// </summary>
      private string SqlFormatInstallSeviceBrokerNotification => $@"
                -- Setup Service Broker
                IF EXISTS (SELECT * FROM sys.databases 
                                    WHERE name = ''{DatabaseName}'' AND (is_broker_enabled = 0 OR is_trustworthy_on = 0)) 
                BEGIN

                    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
                    ALTER DATABASE [{DatabaseName}] SET ENABLE_BROKER; 
                    ALTER DATABASE [{DatabaseName}] SET MULTI_USER WITH ROLLBACK IMMEDIATE

                    -- FOR SQL Express
                    ALTER AUTHORIZATION ON DATABASE::[{DatabaseName}] TO [sa]
                    ALTER DATABASE [{DatabaseName}] SET TRUSTWORTHY ON;

                END

                -- Create a queue which will hold the tracked information 
                IF NOT EXISTS (SELECT * FROM sys.service_queues WHERE name = ''{ConversationQueueName}'')
	                CREATE QUEUE {SchemaName}.[{ConversationQueueName}]
                -- Create a service on which tracked information will be sent 
                IF NOT EXISTS(SELECT * FROM sys.services WHERE name = ''{ConversationServiceName}'')
	                CREATE SERVICE [{ConversationServiceName}] ON QUEUE {SchemaName}.[{ConversationQueueName}] ([DEFAULT]) 
            ";

      /// <summary>
      /// T-SQL script-template which removes database notification.
      /// </summary>
      private string SqlFormatUninstallServiceBrokerNotification => $@"
                DECLARE @serviceId INT
                SELECT @serviceId = service_id FROM sys.services 
                WHERE sys.services.name = ''{ConversationServiceName}''

                DECLARE @ConvHandle uniqueidentifier
                DECLARE Conv CURSOR FOR
                SELECT CEP.conversation_handle FROM sys.conversation_endpoints CEP
                WHERE CEP.service_id = @serviceId AND ([state] != ''CD'' OR [lifetime] > GETDATE() + 1)

                OPEN Conv;
                FETCH NEXT FROM Conv INTO @ConvHandle;
                WHILE (@@FETCH_STATUS = 0) BEGIN
    	            END CONVERSATION @ConvHandle WITH CLEANUP;
                    FETCH NEXT FROM Conv INTO @ConvHandle;
                END
                CLOSE Conv;
                DEALLOCATE Conv;

                -- Droping service and queue.
                DROP SERVICE [{ConversationServiceName}];
                IF OBJECT_ID (''{SchemaName}.{ConversationQueueName}'', ''SQ'') IS NOT NULL
	                DROP QUEUE {SchemaName}.[{ConversationQueueName}];
            ";

      #endregion
      #region Notification Trigger

      /// <summary>
      /// T-SQL script-template which creates notification trigger.
      /// {0} - notification trigger name. 
      /// {1} - schema name.
      /// </summary>
      private string SqlFormatDeleteNotificationTrigger => $@"
                IF OBJECT_ID (''{SchemaName}.{ConversationTriggerName}'', ''TR'') IS NOT NULL
                    DROP TRIGGER {SchemaName}.[{ConversationTriggerName}];
            ";

      private string SqlFormatCheckNotificationTrigger => $@"
                IF OBJECT_ID (''{SchemaName}.{ConversationTriggerName}'', ''TR'') IS NOT NULL
                    RETURN;
            ";

      /// <summary>
      /// T-SQL script-template which creates notification trigger.
      /// {0} - monitorable table name.
      /// {1} - notification trigger name.
      /// {2} - event data (INSERT, DELETE, UPDATE...).
      /// {3} - conversation service name. 
      /// {4} - detailed changes tracking mode.
      /// {5} - schema name.
      /// %inserted_select_statement% - sql code which sets trigger "inserted" value to @retvalOUT variable.
      /// %deleted_select_statement% - sql code which sets trigger "deleted" value to @retvalOUT variable.
      /// </summary>
      private string SqlFormatCreateNotificationTrigger => $@"
                CREATE TRIGGER [{ConversationTriggerName}]
                ON {SchemaName}.[{TableName}]
                AFTER {TriggerTypeByListenerType} 
                AS

                SET NOCOUNT ON;

                --Trigger {TableName} is rising...
                IF EXISTS (SELECT * FROM sys.services WHERE name = ''''{ConversationServiceName}'''')
                BEGIN
                    DECLARE @message NVARCHAR(MAX)
                    SET @message = N''''<root/>''''

                    IF ({(DetailsIncluded ? string.Empty : @"NOT")} EXISTS(SELECT 1))
                    BEGIN
                        DECLARE @retvalOUT NVARCHAR(MAX)

                        %inserted_select_statement%

                        IF (@retvalOUT IS NOT NULL)
                        BEGIN SET @message = N''''<root>'''' + @retvalOUT END                        

                        %deleted_select_statement%

                        IF (@retvalOUT IS NOT NULL)
                        BEGIN
                            IF (@message = N''''<root/>'''') BEGIN SET @message = N''''<root>'''' + @retvalOUT END
                            ELSE BEGIN SET @message = @message + @retvalOUT END
                        END 

                        IF (@message != N''''<root/>'''') BEGIN SET @message = @message + N''''</root>'''' END
                    END

                	--Beginning of dialog...
                	DECLARE @ConvHandle UNIQUEIDENTIFIER
                	--Determine the Initiator Service, Target Service and the Contract 
                	BEGIN DIALOG @ConvHandle 
                        FROM SERVICE [{ConversationServiceName}] TO SERVICE ''''{ConversationServiceName}'''' ON CONTRACT [DEFAULT] WITH ENCRYPTION=OFF, LIFETIME = 60; 
	                --Send the Message
	                SEND ON CONVERSATION @ConvHandle MESSAGE TYPE [DEFAULT] (@message);
	                --End conversation
	                END CONVERSATION @ConvHandle;
                END
            ";
      #endregion
    }
  }
}
