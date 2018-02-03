using System;

namespace Poc.Sqltabledependency {
  [Flags]
  public enum NotificationTypes {
    None = 0,
    Insert = 1 << 1,
    Update = 1 << 2,
    Delete = 1 << 3
  }
}
