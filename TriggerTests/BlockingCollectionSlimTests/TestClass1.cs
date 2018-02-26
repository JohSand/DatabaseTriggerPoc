using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Poc.Sqltabledependency;

namespace TriggerTests.BlockingCollectionSlimTests {
  [TestFixture]
  public class TestClass1 {
    [Test]
    public void TestOneConsumer() {
      var list = new List<int>();

      var collection = new BlockingCollectionSlim<int>(new ConcurrentQueue<int>());


      var thread = new Thread(() =>
      {
        foreach (var i in collection.GetConsumingEnumerable())
        {
          list.Add(i);
        }

   
      });
      thread.Start();
      collection.Add(1);
      Thread.Sleep(200);
      foreach (var i in Enumerable.Range(2, 9)) {

        collection.Add(i);
      }
      collection.CompleteAdding();
      thread.Join(TimeSpan.FromMilliseconds(10));
      Assert.That(list, Is.EquivalentTo(Enumerable.Range(1, 10)));
    }

    [Test]
    public void TestTwoConsumer() {
      var list1 = new List<int>();
      var list2 = new List<int>();

      var collection = new BlockingCollectionSlim<int>(new ConcurrentStack<int>());


      var thread1 = new Thread(() => {
        foreach (var i in collection.GetConsumingEnumerable()) {
          list1.Add(i);
          Thread.Sleep(10);
        }
      });
      thread1.Start();

      var thread2 = new Thread(() => {
        foreach (var i in collection.GetConsumingEnumerable()) {
          list2.Add(i);
          Thread.Sleep(10);
        }
      });
      thread2.Start();

      foreach (var i in Enumerable.Range(1, 50)) {
        collection.Add(i);
      }
      collection.CompleteAdding();



      thread1.Join();
      thread2.Join();
      var result = list1.Concat(list2);

      Assert.That(result, Is.EquivalentTo(Enumerable.Range(1, 50)));
    }
  }
}