using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TrimuiSmartHub.Core.Jobs;

/// <param name="Fraction">0..1, or null when the progress is unknown.</param>
public readonly record struct JobProgress(double? Fraction, string? Message = null);

public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Canceled,
}

/// <summary>A long-running task (copy, scrape, download...) with progress and cancellation.</summary>
public sealed class Job : INotifyPropertyChanged
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly TaskCompletionSource<object?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private JobStatus _status = JobStatus.Queued;
    private double? _progress;
    private string? _message;
    private string? _error;

    internal Job(string title, Func<Job, IProgress<JobProgress>, CancellationToken, Task<object?>> work)
    {
        Title = title;
        Work = work;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; } = Guid.NewGuid();

    public string Title { get; }

    public DateTime CreatedUtc { get; } = DateTime.UtcNow;

    internal Func<Job, IProgress<JobProgress>, CancellationToken, Task<object?>> Work { get; }

    internal CancellationToken CancellationToken => _cancellation.Token;

    public JobStatus Status
    {
        get => _status;
        internal set
        {
            if (Set(ref _status, value)) OnPropertyChanged(nameof(IsActive));
        }
    }

    public bool IsActive => Status is JobStatus.Queued or JobStatus.Running;

    public double? Progress
    {
        get => _progress;
        internal set => Set(ref _progress, value);
    }

    public string? Message
    {
        get => _message;
        internal set => Set(ref _message, value);
    }

    public string? Error
    {
        get => _error;
        internal set => Set(ref _error, value);
    }

    /// <summary>Completes when the job ends; faults/cancels like the job itself.</summary>
    public Task<object?> Completion => _completion.Task;

    public void Cancel()
    {
        if (!IsActive) return;
        _cancellation.Cancel();
        if (Status == JobStatus.Queued) Finish(JobStatus.Canceled, null, new OperationCanceledException());
    }

    internal void Finish(JobStatus status, object? result, Exception? error)
    {
        if (!IsActive && _completion.Task.IsCompleted) return;

        Status = status;
        if (status == JobStatus.Completed) Progress = 1;

        if (status == JobStatus.Completed) _completion.TrySetResult(result);
        else if (status == JobStatus.Canceled) _completion.TrySetCanceled();
        else _completion.TrySetException(error ?? new InvalidOperationException("Job failed."));
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Runs jobs one at a time. Sequential on purpose: parallel copies to the same SD card are slower than
/// sequential ones and make an interrupted sync harder to reason about.
/// </summary>
public sealed class JobQueue : IAsyncDisposable
{
    private readonly Channel<Job> _channel = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ILogger<JobQueue> _logger;
    private readonly Task _worker;
    private readonly List<Job> _jobs = new();
    private readonly object _lock = new();

    public JobQueue(ILogger<JobQueue>? logger = null)
    {
        _logger = logger ?? NullLogger<JobQueue>.Instance;
        _worker = Task.Run(RunAsync);
    }

    /// <summary>Raised (on a worker thread) when a job is enqueued.</summary>
    public event Action<Job>? JobAdded;

    public IReadOnlyList<Job> Jobs
    {
        get
        {
            lock (_lock) return _jobs.ToList();
        }
    }

    public Job Enqueue(string title, Func<IProgress<JobProgress>, CancellationToken, Task> work) =>
        Enqueue<object?>(title, async (progress, ct) =>
        {
            await work(progress, ct);
            return null;
        });

    public Job Enqueue<T>(string title, Func<IProgress<JobProgress>, CancellationToken, Task<T>> work)
    {
        var job = new Job(title, async (_, progress, ct) => await work(progress, ct));
        lock (_lock) _jobs.Add(job);
        JobAdded?.Invoke(job);
        _channel.Writer.TryWrite(job);
        return job;
    }

    public void ClearFinished()
    {
        lock (_lock) _jobs.RemoveAll(j => !j.IsActive);
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(_shutdown.Token))
            {
                if (!job.IsActive) continue;
                await RunJobAsync(job);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task RunJobAsync(Job job)
    {
        job.Status = JobStatus.Running;
        var progress = new InlineProgress<JobProgress>(p =>
        {
            if (p.Fraction.HasValue) job.Progress = Math.Clamp(p.Fraction.Value, 0, 1);
            if (p.Message != null) job.Message = p.Message;
        });

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(job.CancellationToken, _shutdown.Token);
        try
        {
            var result = await job.Work(job, progress, linked.Token);
            job.Finish(JobStatus.Completed, result, null);
        }
        catch (OperationCanceledException ex)
        {
            job.Finish(JobStatus.Canceled, null, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job failed: {Title}", job.Title);
            job.Error = ex.Message;
            job.Finish(JobStatus.Failed, null, ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _shutdown.Cancel();
        try
        {
            await _worker;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
