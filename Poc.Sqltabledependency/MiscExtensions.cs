using System;
using System.Collections.Generic;
using System.Linq;

namespace Poc.Sqltabledependency {
  public static class MiscExtensions {
    public static string StringJoin(this IEnumerable<string> source, string separator) =>
      string.Join(separator, source);

    public static IEnumerable<T> GetValues<T>(Type type) => Enum.GetValues(type).OfType<T>();

    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action) {
      foreach (var x in source) {
        action(x);
      }
    }

    public static void ForEach<T1,T2>(this IEnumerable<T1> source, Func<T1,T2> action) {
      foreach (var x in source) {
        action(x);
      }
    }
  }
}