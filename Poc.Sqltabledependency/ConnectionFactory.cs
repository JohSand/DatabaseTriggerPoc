using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poc.Sqltabledependency {
  public static class ConnectionFactory {
    public static SqlConnectionStringBuilder SqlConnectionStringBuilder => new SqlConnectionStringBuilder {
      InitialCatalog = @"Sandbox",
      DataSource = @"(localdb)\mssqllocaldb",
      AttachDBFilename = @"C:\tfs\SandboxDb\Sandbox.mdf"//todo
    };

    public static SqlConnection GetCreateConnection() => new SqlConnection(SqlConnectionStringBuilder.ToString());
  }
}
