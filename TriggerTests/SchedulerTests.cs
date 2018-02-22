using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using NodaTime.Text;
using NUnit.Framework;
using Poc.Sqltabledependency;


namespace TriggerTests {
  [TestFixture]
  public class SchedulerTests {
    class Thing
    {
      public ZonedDateTime Time { get; set; }
    }

    [Test]
    public void HowDoesTimeWork()
    {
      var s = new JsonSerializerSettings().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
     
      var myString = JsonConvert.SerializeObject(new ZonedDateTime(SystemClock.Instance.GetCurrentInstant(), DateTimeZone.Utc), s);
      Console.WriteLine(myString);
      var dyn = JsonConvert.DeserializeObject(myString, s);
      var newString = (string)dyn.ToString();
      //Console.WriteLine(newString);
      var deser = JsonConvert.DeserializeObject<ZonedDateTime>("'" + newString + "'", s);
      Console.WriteLine(deser);
      var pattern = ZonedDateTimePattern.ExtendedFormatOnlyIso.WithZoneProvider(DateTimeZoneProviders.Serialization);
      Console.WriteLine(pattern.PatternText);
      var res = pattern.Parse(newString).Value;

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
      var scheduler = new SingleThreadTaskScheduler(ApartmentState.STA);
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
          scheduler
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