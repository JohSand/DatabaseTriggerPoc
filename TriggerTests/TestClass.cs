using NUnit.Framework;
using Poc.Sqltabledependency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Dapper;
using TableDependency;
using TableDependency.SqlClient;

namespace TriggerTests {
  [TestFixture]
  public class TestClass {
    //private TransactionScope Scope { get; set; }

    [OneTimeSetUp]
    public void Init() {
      //Scope = new TransactionScope();
      //CreateTables();
    }



    [OneTimeTearDown]
    public void Cleanup() {
      //dispose without completion, this will rollback everything
      //Scope.Complete();
      //Scope.Dispose();
    }

    [Test]
    public void CanConnectToDatabase() {
      var connection = ConnectionFactory.GetCreateConnection();
      connection.Open();
      connection.Close();
      connection.Dispose();
    }

    [Test]
    public void TestInsertTrigger() {
      bool wasChanged = false;
      var autoEvent = new AutoResetEvent(false);
      using (var dep = new SqlDependencyEx(ConnectionFactory.SqlConnectionStringBuilder.ToString(), "Sandbox", "TestTable")) {
        dep.TableChanged += (sender, args) => {
          wasChanged = true;
          autoEvent.Set();
        };

        dep.Start();

        InsertIntoTable();

        autoEvent.WaitOne(TimeSpan.FromSeconds(2));
      }


      Assert.That(wasChanged, Is.True);
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

    [Test]
    public void InsertIntoTable() {
      var insertSql = @"insert into [TestTable] values (@date, @name)";

      using (var connection = ConnectionFactory.GetCreateConnection()) {
        connection.Execute(insertSql, new { date = DateTime.Today, name = "test" });
      }
    }

    //[Test]
    //public void UpdateTable() {
    //  var createTableSql = @"CREATE TABLE TestTable(
    //                           Id int,
    //                           SomeDate datetime,
    //                           SomeText varchar(20),
    //                           PRIMARY KEY( Id )
    //                        );";
    //  using (var connection = ConnectionFactory.GetCreateConnection()) {
    //    connection.Open();

    //    connection.Execute(createTableSql);
    //  }
    //}

    [Test]
    public void TestValues()
    {
      var asd = new SqlDependencyEx.QueueInitializer("TestDataBase", 1, "TestTableName", "dbo", "connectionString",
        true, SqlDependencyEx.NotificationTypes.Insert);
      //Assert.That(asd.GetInstallNotificationProcedureScript2(), Is.EqualTo(asd.GetInstallNotificationProcedureScript()));
    }
  }
}