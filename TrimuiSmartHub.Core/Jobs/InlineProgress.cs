namespace TrimuiSmartHub.Core.Jobs;

/// <summary>An <see cref="IProgress{T}"/> that invokes the handler immediately, in order, on the reporting thread.</summary>
public sealed class InlineProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public InlineProgress(Action<T> handler) => _handler = handler;

    public void Report(T value) => _handler(value);
}
