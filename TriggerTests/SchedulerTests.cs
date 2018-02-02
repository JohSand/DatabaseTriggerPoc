using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;
using NUnit.Framework;
using Poc.Sqltabledependency;

namespace TriggerTests {
  [TestFixture]
  public class SchedulerTests{

    [Test]
    public void HowDoesTimeWork()
    {
      var t1 = DateTime.UtcNow;
      var t1Ticks = t1.Ticks;
      var t2 = new DateTime(t1Ticks);

      Assert.That(t1.Ticks, Is.EqualTo(t2.Ticks));

      Assert.That(t2.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void HowDoesTimeWork2()
    {
      int? test = null;

      if(test is int i)
        Console.WriteLine(i);
      else
        Console.WriteLine("null");
    }


    [Test]
    public void RunOnThread()
    {
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

    [Test]
    public void RunOnThread2()
    {
      var scheduler = new SingleThreadTaskScheduler(ApartmentState.MTA);
      //var factory = new TaskFactory();
      var ctSource = new CancellationTokenSource();
      var task = Task.Factory.StartNew(async () =>
        {
          while (!ctSource.IsCancellationRequested)
          {
            Console.WriteLine($"Went to sleep on thread: {Thread.CurrentThread?.ManagedThreadId ?? 0}");
            await Task.Delay(10, ctSource.Token);
            Console.WriteLine($"Woke up on on thread: {Thread.CurrentThread?.ManagedThreadId ?? 0}");
            Console.WriteLine();
          }
        }, ctSource.Token,
        TaskCreationOptions.None,
        TaskScheduler.Default
      ).Unwrap();
      Task.Delay(100).GetAwaiter().GetResult();
      ctSource.Cancel();
      ctSource.Token.WaitHandle.WaitOne(TimeSpan.FromTicks(10));
      try
      {
        task.GetAwaiter().GetResult();
      }
      catch (TaskCanceledException)
      {
      }
    }
  }
}