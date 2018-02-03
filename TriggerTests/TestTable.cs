using System;

namespace TriggerTests {
  public class TestTable {
    public DateTime SomeDate { get; set; }

    public string SomeText { get; set; }

    public override string ToString() => $"Date: {SomeDate}, Text: {SomeText}";
  }
}