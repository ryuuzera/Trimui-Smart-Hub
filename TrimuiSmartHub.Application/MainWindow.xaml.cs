using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TrimuiSmartHub.Application.Services.Trimui;
using TrimuiSmartHub.Application.Pages;
using MahApps.Metro.Controls;
using ControlzEx.Theming;

namespace TrimuiSmartHub.Application
{
    public partial class MainWindow : MetroWindow
    {
        private TrimuiService trimuiService;
        public MainWindow()
        {
            InitializeComponent();

            ThemeManager.Current.ChangeTheme(this, ThemeManager.Current.GetTheme("Light.Steel"));

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

                return;
            }
        }
    }
}