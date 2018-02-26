using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using Poc.Sqltabledependency;
using Poc.Sqltabledependency.RefactoredVersion;

namespace DatabaseTriggerPoc {
  class Program {
    static int Main(string[] args) {


      var summary = BenchmarkRunner.Run<Benchmark>();
      //var path = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Data");
      //var file = Path.Combine(new DirectoryInfo(path).FullName, "Sandbox.mdf");
      //string connectionString = new SqlConnectionStringBuilder {
      //  DataSource = @"(localdb)\mssqllocaldb",
      //  AttachDBFilename = file
      //}.ToString();


      //var cts = new CancellationTokenSource();
      //var token = cts.Token;
      //var listener = new DbTableListener<Test>(connectionString);

      //listener.TableChanged += (sender, eventArgs) => {
      //  //throw new AggregateException();
      //  switch (eventArgs) {
      //    case RowDeletedEventArgs<Test> rowDeletedEventArgs:
      //      Console.WriteLine("Deleted: " + rowDeletedEventArgs.DeletedRow);
      //      break;
      //    case RowInsertedEventArgs<Test> rowInsertedEventArgs:
      //      Console.WriteLine("Inserted: " + rowInsertedEventArgs.InsertedRow);
      //      break;
      //    case RowUpdatedEventArgs<Test> rowUpdatedEventArgs:
      //      Console.WriteLine("Before: " + rowUpdatedEventArgs.Before);
      //      Console.WriteLine("After: " + rowUpdatedEventArgs.After);
      //      break;
      //  }
      //  Console.WriteLine("");
      //};
      //var t = listener.Start(token);

      //Console.ReadKey();
      //cts.Cancel();
      //await t;
      //Console.WriteLine("canceled");
      //Console.ReadKey();
      return 0;
    }
  }
}