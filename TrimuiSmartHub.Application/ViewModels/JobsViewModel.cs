using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrimuiSmartHub.Core.Jobs;

namespace TrimuiSmartHub.Application.ViewModels;

/// <summary>Background tasks panel. Every long operation in the app goes through the job queue.</summary>
public sealed partial class JobsViewModel : PageViewModel
{
    private readonly JobQueue _queue;
    private readonly Dispatcher _dispatcher;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private Job? _current;

    public JobsViewModel(JobQueue queue)
    {
        _queue = queue;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _queue.JobAdded += job => _dispatcher.BeginInvoke(() => Add(job));
    }

    public ObservableCollection<Job> Jobs { get; } = new();

    public Job Run(string title, Func<IProgress<JobProgress>, CancellationToken, Task> work) => _queue.Enqueue(title, work);

    public Job Run<T>(string title, Func<IProgress<JobProgress>, CancellationToken, Task<T>> work) => _queue.Enqueue(title, work);

    private void Add(Job job)
    {
        Jobs.Insert(0, job);
        job.PropertyChanged += OnJobChanged;
        Refresh();
    }

    private void OnJobChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Job.Status)) _dispatcher.BeginInvoke(Refresh);
    }

    private void Refresh()
    {
        ActiveCount = Jobs.Count(j => j.IsActive);
        Current = Jobs.LastOrDefault(j => j.Status == JobStatus.Running) ?? Jobs.LastOrDefault(j => j.Status == JobStatus.Queued);
    }

    [RelayCommand]
    private static void Cancel(Job? job) => job?.Cancel();

    [RelayCommand]
    private void ClearFinished()
    {
        _queue.ClearFinished();
        foreach (var job in Jobs.Where(j => !j.IsActive).ToList())
        {
            job.PropertyChanged -= OnJobChanged;
            Jobs.Remove(job);
        }
        Refresh();
    }
}
