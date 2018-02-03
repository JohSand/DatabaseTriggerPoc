EXEC sp_detach_db 'Sandbox'

SELECT 'EXEC sp_detach_db ''' + name + ''''
FROM sys.databases
;
use Sandbox;
go;

CREATE TABLE TestTable(
                               Id int primary key IDENTITY(1,1) NOT NULL,
                               SomeDate datetime,
                               SomeText varchar(20)
                            );