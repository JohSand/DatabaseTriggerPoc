using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Poc.Sqltabledependency {
  public sealed class SingleThreadSynchronizationContext : SynchronizationContext {
    private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object>> _queue = new BlockingCollection<KeyValuePair<SendOrPostCallback, object>>();


    public override void Post(SendOrPostCallback d, object state) {
      _queue.Add(new KeyValuePair<SendOrPostCallback, object>(d, state));
    }


    public void RunOnCurrentThread() {
      _queue.GetConsumingEnumerable().ForEach(workItem => workItem.Key(workItem.Value));
    }


    public void Complete() {
      _queue.CompleteAdding();
    }
  }
}