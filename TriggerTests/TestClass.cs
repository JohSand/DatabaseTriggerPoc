using NUnit.Framework;
using Poc.Sqltabledependency;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Dapper;
using Poc.Sqltabledependency.RefactoredVersion;
using TableDependency;
using TableDependency.SqlClient;

namespace TriggerTests {
  [TestFixture]
  public class TestClass {
    private TransactionScope Scope { get; set; }

    [OneTimeSetUp]
    public void Init() {
      var data = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
      Directory.CreateDirectory(data);
      var filename = Path.Combine(data, $"testDb.mdf");
      if (!File.Exists(filename)) {
        CreateSqlDatabase(filename);
        CreateTables();
      }
    }

    //[SetUp]
    //public void Setup() {
    //  Scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
    //}

    //[TearDown]
    //public void TearDown() {
    //  Scope.Dispose();
    //  Scope = null;
    //}

    public static void CreateSqlDatabase(string filename) {
      var databaseName = Path.GetFileNameWithoutExtension(filename);
      using (var connection = new SqlConnection(@"Data Source=(localdb)\mssqllocaldb;")) {
//"
        connection.Open();
        using (var command = connection.CreateCommand()) {
          command.CommandText = 
                $@"
                   USE master;
                   CREATE DATABASE {databaseName} ON PRIMARY (NAME={databaseName}, FILENAME='{filename}');
                   EXEC sp_detach_db '{databaseName}', 'true'; ";
          command.ExecuteNonQuery();
        }
      }
    }

    [OneTimeTearDown]
    public void Cleanup() {
      var data = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");

      var filename = Path.Combine(data, $"testDb.mdf");

      var databaseName = Path.GetFileNameWithoutExtension(filename);

      using (var connection = new SqlConnection(@"Data Source=(localdb)\mssqllocaldb;")) {
//"
        connection.Open();
        using (var command = connection.CreateCommand()) {
          command.CommandText = $@"USE master;
                                    ALTER DATABASE[{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                                    DROP DATABASE[{databaseName}]; ";
          command.ExecuteNonQuery();
        }
      }

      Directory.Delete(data, true);
    }

    [Test]
    public void CanConnectToDatabase() {
      var connection = ConnectionFactory.GetCreateConnection();
      connection.Open();
      connection.Close();
      connection.Dispose();
    }

    private void CreateTables() {
      var createTableSql = @"CREATE TABLE TestTable(
                               Id int primary key IDENTITY(1,1) NOT NULL,
                               SomeDate datetime,
                               SomeText varchar(20)
                            );";
      using (var connection = ConnectionFactory.GetCreateConnection()) {
        connection.Open();
        connection.Execute(createTableSql);
      }
    }

    public void InsertIntoTable() {
      var insertSql = @"insert into [TestTable] values (@date, @name)";

      using (var connection = ConnectionFactory.GetCreateConnection()) {
        connection.Execute(insertSql, new {date = DateTime.Today, name = "test"});
      }
    }

    [Test]
    public async Task TestValues() {
      var connectionString = @"Data Source=(localdb)\mssqllocaldb;";
      var installer = new ListenerInstaller( "TestTable", "testDb");
      installer.InstallListener(connectionString);
      bool wasChanged = false;

      var cts = new CancellationTokenSource();
      var token = cts.Token;
      var listener = new DbTableListener<TestTable>(new SqlConnectionStringBuilder {
        InitialCatalog = @"testDb",
        DataSource = @"(localdb)\mssqllocaldb",
        AttachDBFilename = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "testDb.mdf")//todo
      }.ToString());

      listener.TableChanged += (sender, eventArgs) => {
        //throw new AggregateException();
        switch (eventArgs) {
          case RowDeletedEventArgs<TestTable> rowDeletedEventArgs:
            Console.WriteLine("Deleted: " + rowDeletedEventArgs.DeletedRow);
            break;
          case RowInsertedEventArgs<TestTable> rowInsertedEventArgs:
            Console.WriteLine("Inserted: " + rowInsertedEventArgs.InsertedRow);
            break;
          case RowUpdatedEventArgs<TestTable> rowUpdatedEventArgs:
            Console.WriteLine("Before: " + rowUpdatedEventArgs.Before);
            Console.WriteLine("After: " + rowUpdatedEventArgs.After);
            break;
        }
        wasChanged = true;
       cts.Cancel();

      };
      var t = listener.Start(token);
      InsertIntoTable();

      await t;
      Assert.True(wasChanged);
      installer.UninstallListener(@"Data Source=(localdb)\mssqllocaldb;");
    }
  }
}