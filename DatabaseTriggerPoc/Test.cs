using System;

namespace DatabaseTriggerPoc {
  public class Test {
    public int Id { get; set; }
    public DateTime SomeDate { get; set; }
    public string SomeText { get; set; }

    public override string ToString() {
      return $"Id: {Id}, SomeTime: {SomeDate}, SomeText: {SomeText}";
    }
  }


}