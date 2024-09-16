using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TrimuiSmartHub.Application.Services.Trimui;

namespace TrimuiSmartHub.Application.Pages
{
    /// <summary>
    /// Interação lógica para Home.xam
    /// </summary>
    public partial class Home : Page
    {
        TrimuiService trimuiService;

        MainWindow mainWindow;
        public Home(MainWindow MainWindow)
        {   
            InitializeComponent();

            trimuiService = TrimuiService.New();

            mainWindow = MainWindow;

            DataContext = trimuiService;

            UpdateImageSource(trimuiService.Status);

            trimuiService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(trimuiService.Status))
                {
                    Dispatcher.Invoke(() => UpdateImageSource(trimuiService.Status));
                }
            };
        }
        private void UpdateImageSource(string status)
        {
            TrimuiImg.Source = new BitmapImage(new Uri($"pack://application:,,,/Resources/Images/{(status.Contains("Dis") ? "trimui_off" : "trimui_on")}.png"));
        }

        private void TrimuiButton_Click(object sender, RoutedEventArgs e)
        {
            if (trimuiService.Status.Equals("Connected")) {
                mainWindow.MainFrame.Navigate(new SmartProHub(mainWindow));

                return;
            };
        }
    }
}
