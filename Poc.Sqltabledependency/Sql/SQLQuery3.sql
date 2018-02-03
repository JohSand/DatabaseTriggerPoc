insert into [TestTable] values (GETDATE(), 'test3')


update [TestTable] set SomeText = 'asdaf' where Id = 5

delete from TestTable where Id = 9

DECLARE @ConvHandle UNIQUEIDENTIFIER
DECLARE @message VARBINARY(MAX)
WAITFOR 
(
    RECEIVE TOP(1) 
    @ConvHandle=Conversation_Handle,
    @message=message_body 
	FROM dbo.[EventQueue]
), Timeout 20;

BEGIN
    END CONVERSATION @ConvHandle; 
END
SELECT CAST(@message AS NVARCHAR(MAX)) 


      CREATE TRIGGER [dbo].tr_Listener 
      ON [testDb].[dbo].[TestTable] 
      AFTER INSERT, UPDATE, DELETE 
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
              FROM SERVICE [ListenerService]   
              TO SERVICE 'ListenerService' 
              ON CONTRACT [EventContract]  
              WITH ENCRYPTION = OFF;   
              SEND ON CONVERSATION @Handle   
              MESSAGE TYPE [EventMessage](@message);
          END
      GO