using System;

namespace Poc.Sqltabledependency.RefactoredVersion {
  public class TableChangedEventArgs<T> : EventArgs {
    public override string ToString() {
      return this.GetType().FullName;
    }
  }

  public class RowInsertedEventArgs<T> : TableChangedEventArgs<T> {
    public T InsertedRow { get; set; }
  }

  public class RowUpdatedEventArgs<T> : TableChangedEventArgs<T> {
    public T After { get; set; }

    public T Before { get; set; }
  }

  public class RowDeletedEventArgs<T> : TableChangedEventArgs<T> {
    public T DeletedRow { get; set; }
  }
}