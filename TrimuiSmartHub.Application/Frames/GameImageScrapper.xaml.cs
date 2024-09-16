using System.Windows;
using System.Windows.Controls;
using TrimuiSmartHub.Application.Services.Trimui;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using TrimuiSmartHub.Application.Repository;
using CsQuery.ExtensionMethods.Internal;

namespace TrimuiSmartHub.Application.Frames
{
    public partial class GameImageScrapper : Page
    {
        public GameImageScrapper()
        {
            InitializeComponent();

            PopulateEmulators();

        }

        private void PopulateEmulators()
        {
            var emulatorList = TrimuiService.New().GetEmulators();

            foreach (var emulator in emulatorList)
            {
                var emulatorCard = CreateGameComponent(emulator);

                if (emulatorCard == null) continue;

                Container.Children.Add(emulatorCard);
            }
        }
        private Button? CreateGameComponent(string emulator, string gamesTotal = "0")
        {
            string emulatorDescription;

            EmulatorDictionary.EmulatorMap.TryGetValue(emulator, out emulatorDescription);

            if (emulatorDescription.IsNullOrEmpty()) return null;

            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(110, 51, 51, 51)), // Cor com transparência
                CornerRadius = new CornerRadius(10),
                Width = 120,
                Height = 120,
                Margin = new Thickness(5)
            };

            StackPanel stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            Image image = new Image
            {
                Source = new BitmapImage(new Uri($"pack://application:,,,/Resources/Images/Emulators/{emulator}.png")),
                Width = 70,
                Height = 60,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };

            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            TextBlock titleBlock = new TextBlock
            {
                Text = emulator,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            TextBlock descriptionBlock = new TextBlock
            {
                Text = $"{gamesTotal} Games",
                FontSize = 12,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(image);
            stackPanel.Children.Add(titleBlock);
            stackPanel.Children.Add(descriptionBlock);

            border.Child = stackPanel;

            Button button = new Button
            {
                Content = border,
                Style = (Style)System.Windows.Application.Current.FindResource("GameButtonStyle"), 
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };

            button.Click += (sender, e) =>
            {
                MessageBox.Show($"{emulator} clicked!"); 
            };

            return button;
        }
    }
}
