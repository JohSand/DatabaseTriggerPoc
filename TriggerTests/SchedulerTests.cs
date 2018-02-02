using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Poc.Sqltabledependency;

namespace TriggerTests {
  [TestFixture]
  public class SchedulerTests {
    [Test]
    public void RunOnThread() {
      var syncCtx = new SingleThreadSynchronizationContext();

      SynchronizationContext.SetSynchronizationContext(syncCtx);
      
      Console.WriteLine($"The test runs on thread: {Thread.CurrentThread?.ManagedThreadId ?? 0}");
      Console.WriteLine($"Context: {SynchronizationContext.Current?.ToString() ?? "None"}");
      Console.WriteLine($"Scheduler: {TaskScheduler.Current?.ToString() ?? "None"}");

      var scheduler = new SingleThreadTaskScheduler(ApartmentState.MTA);
      //var factory = new TaskFactory();
      var tasks = Enumerable.Range(1, 4).Select(i => Task.Factory.StartNew(async () => {
        Console.WriteLine($"Task: {i}");
        Console.WriteLine($"Before the await, {i} is on thread: {Thread.CurrentThread?.ManagedThreadId ?? 0}");
        await Task.Delay(10);
        Console.WriteLine($"After the await, {i} is on thread: {Thread.CurrentThread?.ManagedThreadId ?? 0}");
        Console.WriteLine();
      }, CancellationToken.None,
        TaskCreationOptions.None,
       scheduler
        ).Unwrap());

      Task.WhenAll(tasks).GetAwaiter().GetResult();
    }
  }
}