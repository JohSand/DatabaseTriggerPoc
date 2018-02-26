using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using Poc.Sqltabledependency;

namespace DatabaseTriggerPoc {
  [ClrJob(isBaseline: true)]
  public class Benchmark {
    [Params(8)] public int Consumers;

    [Params(300)] public int Workitems;

    private Thread[] workerThreads;

    [Params(500, 2000)] public int Duration;

    [Params(0, 2, 8)] public int Burst;

    [GlobalSetup]
    public void Setup() {
      workerThreads = new Thread[Consumers];
    }


    [Benchmark]
    public int Slim() {
      var c = 0;
      var collection = new BlockingCollectionSlim<int>(new ConcurrentStack<int>());
      for (var i = 0; i < workerThreads.Length; i++) {
        workerThreads[i] = new Thread(() => DoWork(collection, ref c));
        workerThreads[i].Start();
      }

      var rand = new Random();
      foreach (var i in Enumerable.Range(1, Workitems)) {
        if(rand.Next(1, 10) <= 1) {
          Thread.Sleep(TimeSpan.FromTicks(Duration));
        }

        collection.Add(i);
      }

      collection.CompleteAdding();
      foreach (var workerThread in workerThreads) {
        workerThread.Join();
      }

      return c;
    }

    private int DoWork(dynamic collection, ref int c) {
      foreach (var i1 in collection.GetConsumingEnumerable()) {

        c += i1;
      }

      return c;
    }

    [Benchmark]
    public int Fat() {
      var c = 0;
      var collection = new BlockingCollection<int>(new ConcurrentStack<int>());
      for (var i = 0; i < workerThreads.Length; i++) {
        workerThreads[i] = new Thread(() => DoWork(collection, ref c));
        workerThreads[i].Start();
      }

      var rand = new Random();
      foreach (var i in Enumerable.Range(1, Workitems)) {
        if(rand.Next(1, 10) <= Burst)
          Thread.Sleep(TimeSpan.FromTicks(Duration));
        collection.Add(i);
      }

      collection.CompleteAdding();
      foreach (var workerThread in workerThreads) {
        workerThread.Join();
      }

      return c;
    }
  }
}