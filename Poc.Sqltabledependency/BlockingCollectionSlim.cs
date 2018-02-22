using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Poc.Sqltabledependency {
  public class BlockingCollectionSlim<T> {
    private struct ConsumingEnumerable : IEnumerable<T> {
      private readonly BlockingCollectionSlim<T> _collection;

      public ConsumingEnumerable(BlockingCollectionSlim<T> collection) => _collection = collection;

      public IEnumerator<T> GetEnumerator() => new ConsumingEnumerator(_collection);

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private struct ConsumingEnumerator : IEnumerator<T> {
      private readonly BlockingCollectionSlim<T> _collection;
      private T _current;

      public ConsumingEnumerator(BlockingCollectionSlim<T> collection) {
        _collection = collection;
        _current = default;
      }

      public void Dispose() { }

      public bool MoveNext() {
        do {
          if (_collection.Take(out _current))
            return true;
        } while (!_collection._completeAdding);


        return _collection.Take(out _current);
      }

      public void Reset() { }

      public T Current => _current;

      object IEnumerator.Current => Current;
    }

    private readonly IProducerConsumerCollection<T> _queue;
    private readonly ManualResetEventSlim _manualResetEvent = new ManualResetEventSlim(false);

    public IEnumerable<T> GetConsumingEnumerable() => new ConsumingEnumerable(this);

    /// <summary>
    /// Marks the <see cref="T:System.Collections.Concurrent.BlockingCollection{T}"/> instances
    /// as not accepting any more additions.  
    /// </summary>
    /// <remarks>
    /// After a collection has been marked as complete for adding, adding to the collection is not permitted 
    /// and attempts to remove from the collection will not wait when the collection is empty.
    /// </remarks>
    /// <exception cref="T:System.ObjectDisposedException">The <see
    /// cref="T:System.Collections.Concurrent.BlockingCollection{T}"/> has been disposed.</exception>
    public void CompleteAdding() {
      lock (_queue) {
        _completeAdding = true;
      }
      _manualResetEvent.Set();
      //cancel waiting producers
    }

    public BlockingCollectionSlim(IProducerConsumerCollection<T> producerConsumerCollection) {
      _queue = producerConsumerCollection;
    }

    public void Add(T item) {
      _queue.TryAdd(item);
      if (_completeAdding)
        throw new AccessViolationException();
      _manualResetEvent.Set();
    }

    public bool Take(out T item) {
      if (_queue.TryTake(out item))
        return true;

      if (!_completeAdding)
        _manualResetEvent.Wait();

      return false;
    }

    public bool TryTake(out T item, TimeSpan patience) {
      if (_queue.TryTake(out item))
        return true;
      var stopwatch = Stopwatch.StartNew();

      while (stopwatch.Elapsed < patience) {
        if (_queue.TryTake(out item))
          return true;
        var patienceLeft = (patience - stopwatch.Elapsed);
        if (patienceLeft <= TimeSpan.Zero)
          break;
        else if (patienceLeft < MinWait)
          // otherwise the while loop will degenerate into a busy loop,
          // for the last millisecond before patience runs out
          patienceLeft = MinWait;
        _manualResetEvent.Wait(patienceLeft);
      }
      return false;
    }

    private static readonly TimeSpan MinWait = TimeSpan.FromMilliseconds(1);
    private bool _completeAdding;
  }
}