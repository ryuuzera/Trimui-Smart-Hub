using CommunityToolkit.Mvvm.ComponentModel;

namespace TrimuiSmartHub.Application.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    /// <summary>Called every time the page is shown, to refresh data that may have changed elsewhere.</summary>
    public virtual void OnNavigatedTo()
    {
    }
}
