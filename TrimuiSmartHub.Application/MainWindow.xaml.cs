using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TrimuiSmartHub.Application.Services.Trimui;
using TrimuiSmartHub.Application.Pages;

namespace TrimuiSmartHub.Application
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TrimuiService trimuiService;
        public MainWindow()
        {
            InitializeComponent();

            trimuiService = TrimuiService.New();

            DataContext = trimuiService;

            MainFrame.Navigate(new Home(this));

            trimuiService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(trimuiService.Status))
                {
                    Dispatcher.Invoke(() => BackToHome(trimuiService.Status));
                }
            };
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void BackToHome(string status) { 
            if (status.Contains("Dis"))
            {
                MainFrame.Navigate(new Home(this));
            }
        }
    }
}