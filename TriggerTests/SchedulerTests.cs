using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using NodaTime;
using NUnit.Framework;
using Poc.Sqltabledependency;


namespace TriggerTests {
  [TestFixture]
  public class SchedulerTests {


    [Test]
    public void HowDoesTimeWork() {
      var t1 = DateTime.UtcNow;
      var t1Ticks = t1.Ticks;
      var t2 = new DateTime(t1Ticks);

      Assert.That(t1.Ticks, Is.EqualTo(t2.Ticks));

      Assert.That(t2.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public async Task HowDoesTimeWork2() {

      var scheduler = new SingleThreadTaskScheduler(ApartmentState.MTA);
      Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
      var t = Task.Factory.StartNew(() => {
        Console.WriteLine("inside" + Thread.CurrentThread.ManagedThreadId);
        throw new ArgumentException();
      },

      CancellationToken.None,
        TaskCreationOptions.None,
      scheduler: scheduler);
      //var t2 = t.ContinueWith(task => {
      //    if (task.Exception != null)
      //    throw task.Exception;
      //  },
      //  CancellationToken.None,
      //  TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,        
      //  TaskScheduler.Default);

      try {
        await t;//this will use the regular threadpool scheduler
      }
      catch (Exception e) {
        Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
      }
    }


    [Test]
    public void RunOnThread() {
      var scheduler = new SingleThreadTaskScheduler(ApartmentState.MTA);
      //var factory = new TaskFactory();

      var tasks = Enumerable.Range(1, 4)
        .Select(i => Task.Factory.StartNew(async () => {
          Console.WriteLine($"Before the await, {i} is on thread: {Thread.CurrentThread?.ManagedThreadId ?? 0}");
          Console.WriteLine();
          await Task.Delay(10);
          Console.WriteLine($"After the await, {i} is on thread: {Thread.CurrentThread?.ManagedThreadId ?? 0}");
          Console.WriteLine();
        },
            CancellationToken.None,
            TaskCreationOptions.None,
            scheduler
          //TaskScheduler.Default
          )
          .Unwrap());

      Task.WhenAll(tasks).GetAwaiter().GetResult();
    }

    [Test]
    public void RunOnThread2() {
      var scheduler = new SingleThreadTaskScheduler(ApartmentState.MTA);
      //var factory = new TaskFactory();
      var ctSource = new CancellationTokenSource();
      var task = Task.Factory.StartNew(async () => {
        while (!ctSource.IsCancellationRequested) {
          Console.WriteLine($"Went to sleep on thread: {Thread.CurrentThread?.ManagedThreadId ?? 0}");
          await Task.Delay(10, ctSource.Token);
          Console.WriteLine($"Woke up on on thread: {Thread.CurrentThread?.ManagedThreadId ?? 0}");
          Console.WriteLine();
        }
      },
          ctSource.Token,
          TaskCreationOptions.None,
          TaskScheduler.Default
        )
        .Unwrap();
      Task.Delay(100).GetAwaiter().GetResult();
      ctSource.Cancel();
      ctSource.Token.WaitHandle.WaitOne(TimeSpan.FromTicks(10));
      try {
        task.GetAwaiter().GetResult();
      }
      catch (TaskCanceledException) { }
    }
  }
}