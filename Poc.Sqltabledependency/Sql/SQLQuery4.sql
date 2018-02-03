alter TRIGGER [dbo].ddl_trig_database4   
ON [dbo].[TestTable] 
AFTER UPDATE, DELETE, INSERT  
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
        FROM SERVICE EventService   
        TO SERVICE 'EventService' 
        ON CONTRACT [EventContract]  
        WITH ENCRYPTION = OFF;   
        SEND ON CONVERSATION @Handle   
        MESSAGE TYPE [EventMessage](@message);
    END
GO 
