using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Poc.Sqltabledependency {
  public class BlockingCollectionSlim<T> {
    private struct ConsumingEnumerable : IEnumerable<T>, IEnumerator<T> {
      private readonly BlockingCollectionSlim<T> _collection;
      private readonly CancellationToken _token;
      private T _current;
      public ConsumingEnumerable(BlockingCollectionSlim<T> collection, CancellationToken token) {
        _collection = collection;
        _token = token;
        _current = default;
      }

      public IEnumerator<T> GetEnumerator() => this;

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public void Dispose() { /**/ }

      public bool MoveNext() {
        do {
          if (_collection.Take(out _current)) {
            Console.WriteLine("got " + _current);
            return true;
          }

          _collection._manualResetEvent.Wait(_token);
        }
        while (!_collection._completeAdding);


        return _collection.Take(out _current);
      }

      public void Reset() => throw new NotSupportedException("");

      public T Current => _current;

      object IEnumerator.Current => Current;
    }

    private readonly IProducerConsumerCollection<T> _queue;
    private readonly ManualResetEventSlim _manualResetEvent;
    private volatile bool _completeAdding;

    public BlockingCollectionSlim(IProducerConsumerCollection<T> producerConsumerCollection) {
      _queue = producerConsumerCollection;
      _manualResetEvent = new ManualResetEventSlim(false);
    }

    public IEnumerable<T> GetConsumingEnumerable() => GetConsumingEnumerable(CancellationToken.None);

    public IEnumerable<T> GetConsumingEnumerable(CancellationToken token) => new ConsumingEnumerable(this, token);

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
      _completeAdding = true;
      _manualResetEvent.Set();
      //cancel waiting producers
    }



    public void Add(T item) {
      if (_completeAdding)
        throw new AccessViolationException();
      _queue.TryAdd(item);

      _manualResetEvent.Set();
      _manualResetEvent.Reset();
    }

    public bool Take(out T item) => _queue.TryTake(out item);
  }
}