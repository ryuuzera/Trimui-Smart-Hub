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
using TrimuiSmartHub.Application.Frames;

namespace TrimuiSmartHub.Application.Pages
{
    public partial class ConnectionGuide : Page
    {
        private readonly MainWindow _mainWindow;
        public ConnectionGuide(MainWindow mainWindow)
        {
            InitializeComponent();

            _mainWindow = mainWindow;
        }
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.MainFrame.Navigate(new Home(_mainWindow));
        }
    }
}
