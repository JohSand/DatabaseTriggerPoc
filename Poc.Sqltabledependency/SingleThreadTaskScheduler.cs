using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Poc.Sqltabledependency {
  /// <summary>
  ///     Represents a <see cref="TaskScheduler"/> which executes code on a dedicated, single thread whose <see cref="ApartmentState"/> can be configured.
  /// https://github.com/matthiaswelz/journeyofcode/blob/master/SingleThreadScheduler/SingleThreadScheduler/SingleThreadTaskScheduler.cs
  /// </summary>
  /// <remarks>
  ///     You can use this class if you want to perform operations on a non thread-safe library from a multi-threaded environment.
  /// </remarks>
  public sealed class SingleThreadTaskScheduler : TaskScheduler, IDisposable {
    private readonly Thread _thread;
    private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
    private readonly BlockingCollection<Task> _tasks = new BlockingCollection<Task>();
    private readonly Action _initAction;

    /// <summary>
    ///     The <see cref="System.Threading.ApartmentState"/> of the <see cref="Thread"/> this <see cref="SingleThreadTaskScheduler"/> uses to execute its work.
    /// </summary>
    public ApartmentState ApartmentState { get; }

    /// <summary>
    ///     Indicates the maximum concurrency level this <see cref="T:System.Threading.Tasks.TaskScheduler"/> is able to support.
    /// </summary>
    /// 
    /// <returns>
    ///     Returns <c>1</c>.
    /// </returns>
    public override int MaximumConcurrencyLevel => 1;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SingleThreadTaskScheduler"/>, optionally setting an <see cref="System.Threading.ApartmentState"/>.
    /// </summary>
    /// <param name="apartmentState">
    ///     The <see cref="ApartmentState"/> to use. Defaults to <see cref="System.Threading.ApartmentState.STA"/>
    /// </param>
    public SingleThreadTaskScheduler(ApartmentState apartmentState = ApartmentState.STA) : this(null, apartmentState) { }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SingleThreadTaskScheduler"/> passsing an initialization action, optionally setting an <see cref="System.Threading.ApartmentState"/>.
    /// </summary>
    /// <param name="initAction">
    ///     An <see cref="Action"/> to perform in the context of the <see cref="Thread"/> this <see cref="SingleThreadTaskScheduler"/> uses to execute its work after it has been started.
    /// </param>
    /// <param name="apartmentState">
    ///     The <see cref="ApartmentState"/> to use. Defaults to <see cref="System.Threading.ApartmentState.STA"/>
    /// </param>
    public SingleThreadTaskScheduler(Action initAction, ApartmentState apartmentState = ApartmentState.STA)  {
      if (apartmentState != ApartmentState.MTA && apartmentState != ApartmentState.STA)
        throw new ArgumentException(nameof(apartmentState));
      ApartmentState = apartmentState;
      _initAction = initAction ?? (() => { });

      void RunEventLoop() {
        try {
          _initAction();
          //main loop
          _tasks
            .GetConsumingEnumerable(_tokenSource.Token)
            .ForEach(TryExecuteTask);
        }
        catch (OperationCanceledException) {

        }
        finally {
          _tasks.Dispose();
        }
      }
      _thread = new Thread(RunEventLoop) { IsBackground = true };
      _thread.TrySetApartmentState(ApartmentState);
      _thread.Start();
    }


    /// <summary>
    ///     Waits until all scheduled <see cref="Task"/>s on this <see cref="SingleThreadTaskScheduler"/> have executed and then disposes this <see cref="SingleThreadTaskScheduler"/>.
    /// </summary>
    /// <remarks>
    ///     Calling this method will block execution. It should only be called once.
    /// </remarks>
    /// <exception cref="TaskSchedulerException">
    ///     Thrown when this <see cref="SingleThreadTaskScheduler"/> already has been disposed by calling either <see cref="Wait"/> or <see cref="Dispose"/>.
    /// </exception>
    public void Wait() {
      VerifyNotCancelled();

      _tasks.CompleteAdding();
      _thread.Join();
      _tokenSource.Cancel();
    }

    public void Dispose() {
      if (_tokenSource.IsCancellationRequested)
        return;

      _tasks.CompleteAdding();
      _tokenSource.Cancel();
    }


    /// <inheritdoc />
    protected override void QueueTask(Task task) {
      VerifyNotCancelled();      
      _tasks.Add(task, _tokenSource.Token);
    }

    protected override bool TryDequeue(Task task) {
      return false;
    }



    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
      VerifyNotCancelled();

      return _thread == Thread.CurrentThread && !_tokenSource.IsCancellationRequested && TryExecuteTask(task);
    }

    protected override IEnumerable<Task> GetScheduledTasks() {
      VerifyNotCancelled();

      return _tasks.ToArray();
    }



    private void VerifyNotCancelled() {
      if (_tokenSource.IsCancellationRequested)
        throw new TaskSchedulerException(typeof(SingleThreadTaskScheduler).Name);
    }
  }
}