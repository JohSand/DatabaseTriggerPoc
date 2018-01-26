using System;
using System.Collections.Generic;
using System.Linq;

namespace Poc.Sqltabledependency
{
  public static class MiscExtensions
  {
    public static string StringJoin(this IEnumerable<string> source, string separator) =>
      string.Join(separator, source);

    public static IEnumerable<T> GetValues<T>(Type type) => Enum.GetValues(type).OfType<T>();
  }
}