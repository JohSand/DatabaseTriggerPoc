using System.Data.SqlClient;
using System.IO;
using NUnit.Framework;

namespace TriggerTests {
  public static class ConnectionFactory {
    public static SqlConnectionStringBuilder SqlConnectionStringBuilder => new SqlConnectionStringBuilder {
      InitialCatalog = @"testDb",
      DataSource = @"(localdb)\mssqllocaldb",
      AttachDBFilename = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "testDb.mdf")//todo
    };

    public static SqlConnection GetCreateConnection() => new SqlConnection(SqlConnectionStringBuilder.ToString());
  }
}
