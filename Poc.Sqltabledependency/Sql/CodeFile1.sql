-- Create processing procedure for processing queue
CREATE PROCEDURE dbo.ProcessingQueueActivation
AS
BEGIN
  SET NOCOUNT ON;
 
  DECLARE @conversation_handle UNIQUEIDENTIFIER;
  DECLARE @message_body XML;
  DECLARE @message_type_name sysname;
 
  WHILE (1=1)
  BEGIN
    BEGIN TRANSACTION;
 
    WAITFOR
    (
      RECEIVE TOP (1)
        @conversation_handle = conversation_handle,
        @message_body = CAST(message_body AS XML),
        @message_type_name = message_type_name
      FROM ProcessingQueue
    ), TIMEOUT 5000;
 
    IF (@@ROWCOUNT = 0)
    BEGIN
      ROLLBACK TRANSACTION;
      BREAK;
    END
 
    IF @message_type_name = N'AsyncRequest'
    BEGIN
      -- Handle complex long processing here
      -- For demonstration we'll pull the account number and send a reply back only
 
      DECLARE @AccountNumber INT = @message_body.value('(AsyncRequest/AccountNumber)[1]', 'INT');
 
      -- Build reply message and send back
      DECLARE @reply_message_body XML = N'
        ' + CAST(@AccountNumber AS NVARCHAR(11)) + '
      ';
 
      SEND ON CONVERSATION @conversation_handle
        MESSAGE TYPE [AsyncResult] (@reply_message_body);
    END
 
    -- If end dialog message, end the dialog
    ELSE IF @message_type_name = N'http://schemas.microsoft.com/SQL/ServiceBroker/EndDialog'
    BEGIN
      END CONVERSATION @conversation_handle;
    END
 
    -- If error message, log and end conversation
    ELSE IF @message_type_name = N'http://schemas.microsoft.com/SQL/ServiceBroker/Error'
    BEGIN
      -- Log the error code and perform any required handling here
      -- End the conversation for the error
      END CONVERSATION @conversation_handle;
    END
 
    COMMIT TRANSACTION;
  END
END
GO


CREATE TRIGGER [dbo].ddl_trig_database2   
ON [Sandbox].[dbo].[TestTable] 
AFTER UPDATE   
AS   
	DECLARE @message NVARCHAR(MAX)
	set @message = (SELECT * FROM inserted for xml AUTO)
    PRINT 'update' + @message
GO 


alter TRIGGER [dbo].ddl_trig_database4   
ON [Sandbox].[dbo].[TestTable] 
AFTER UPDATE, DELETE  
AS   
	DECLARE @message NVARCHAR(MAX)
	IF EXISTS(SELECT * FROM inserted)
	begin
		set @message = (SELECT * FROM inserted for xml AUTO)
		PRINT 'update' + @message
	end
	else
		PRINT 'nothing'
	

	DECLARE @message1 NVARCHAR(MAX)
	set @message1 = (SELECT * FROM deleted for xml AUTO)
    PRINT 'deleted' + @message1
GO 
