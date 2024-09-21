using System.Windows;
using System.Windows.Controls;
using TrimuiSmartHub.Application.Frames;

namespace TrimuiSmartHub.Application.Pages
{
    public partial class SmartProHub : Page
    {
        private readonly MainWindow _mainWindow;

        private readonly Stack<Page> _navigationHistory = new Stack<Page>();

        public SmartProHub(MainWindow mainWindow)
        {
            InitializeComponent();

            _mainWindow = mainWindow;

            var initialPage = new TrimuiSmartHubLogo();

            NavigationFrame.Navigate(initialPage);

            _navigationHistory.Push(initialPage);
        }

        private void BackHome_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationFrame.Content is TrimuiSmartHubLogo)
            {
                _mainWindow.MainFrame.Navigate(new Home(_mainWindow));
                return;
            }

            if (_navigationHistory.Count > 1)
            {
                _navigationHistory.Pop();
                var previousPage = _navigationHistory.Peek();
                NavigationFrame.Navigate(previousPage);
            }
        }

        private void GetImages_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new GameImageScrapper());
        }

        private void Roms_Click(object sender, RoutedEventArgs e)
        {
            NavigateToPage(new GameDownloader(this));
        }

        private void NavigateToPage(Page newPage)
        {
            if (NavigationFrame.Content is Page currentPage && currentPage != newPage)
            {
                _navigationHistory.Push(newPage);
                NavigationFrame.Navigate(newPage);
            }
        }
    }
}
