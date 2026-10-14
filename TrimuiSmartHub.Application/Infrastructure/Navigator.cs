using TrimuiSmartHub.Application.ViewModels;

namespace TrimuiSmartHub.Application.Infrastructure;

/// <summary>Lets pages navigate to each other without depending on the main window view model.</summary>
public sealed class Navigator
{
    public event Action<Type>? NavigationRequested;

    public void GoTo<T>() where T : PageViewModel => NavigationRequested?.Invoke(typeof(T));
}
