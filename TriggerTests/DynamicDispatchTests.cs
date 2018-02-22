using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TriggerTests {
  public class DynamicDispatchTests {
    [Test]
    public void DynamicDispatchesToParentIfNoMoreSpecificTypeIsFound() {
      dynamic c = GetClass("c");
      Assert.That(Test(c), Is.EqualTo("I am generic"));
    }



        [Test]
    public void DynamicDispatchesToParentIfNotDowncast() {
      var c = GetClass("a");
      Assert.That(Test(c), Is.EqualTo("I am generic"));
    }

            [Test]
    public void DynamicDispatchesToMostSpecificIfDynamic() {
      dynamic c = GetClass("a");
      Assert.That(Test(c), Is.EqualTo("I am a"));
    }

    public AbstractBase GetClass(string t) {
      switch (t) {
        case "a": return new A();
        case "b": return new B();
        case "c": return new C();
        default: return null;
      }
    }

    public string Test(AbstractBase a) {
      return "I am generic";
    }

    public string Test(A a) {
      return "I am a";
    }

    public string Test(B a) {
      return "I am b";
    }
  }

  public abstract class AbstractBase { }
  public class A : AbstractBase { }

  public class B : AbstractBase { }

  public class C : AbstractBase { }
}
