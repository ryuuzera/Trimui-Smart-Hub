using System.Windows;
using System.Windows.Controls;
using TrimuiSmartHub.Application.Frames;

namespace TrimuiSmartHub.Application.Pages
{
    public partial class SmartProHub : Page
    {
        private readonly MainWindow _mainWindow;
        public SmartProHub(MainWindow mainWindow)
        {
            InitializeComponent();

            _mainWindow = mainWindow;

            NavigationFrame.Navigate(new TrimuiSmartHubLogo());
        }

        private void BackHome_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.MainFrame.Navigate(new Home(_mainWindow));
        }

        private void GetImages_Click(object sender, RoutedEventArgs e)
        {
            NavigationFrame.Navigate(new GameImageScrapper());
        }
    }
}
