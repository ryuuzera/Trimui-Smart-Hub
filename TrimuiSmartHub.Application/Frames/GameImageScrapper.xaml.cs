using System.Windows;
using System.Windows.Controls;
using TrimuiSmartHub.Application.Services.Trimui;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using TrimuiSmartHub.Application.Repository;
using CsQuery.ExtensionMethods.Internal;
using TrimuiSmartHub.Application.Services.LibRetro;

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
                var emulatorRoms = TrimuiService.New().GetRomsByEmulator(emulator);

                var emulatorCard = CreateGameComponent(emulator, emulatorRoms);

                if (emulatorCard == null) continue;

                Container.Children.Add(emulatorCard);
            }
        }
        private Button? CreateGameComponent(string emulator, List<string> romsList)
        {
            string emulatorDescription;

            EmulatorDictionary.EmulatorMap.TryGetValue(emulator, out emulatorDescription);

            if (emulatorDescription.IsNullOrEmpty()) return null;

            Border border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(110, 51, 51, 51)), 
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
                Text = $"{romsList.Count} Games",
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
                var imgFolder = TrimuiService.New().GetImageFolder(emulator);

                var count = 0;
                foreach (var rom in romsList)
                {
                    if (File.Exists(Path.Combine(imgFolder, $"{rom}.png"))) continue;

                    var boxImage = LibretroService.New().SearchThumbnail(emulatorDescription, rom);

                    if (boxImage == null) continue;
                   
                    File.WriteAllBytes($@"{imgFolder}\{rom}.png", boxImage);
                    
                    count++;
                }

                if (count > 0) MessageBox.Show($"{count} new Game Images find!");
            };

            return button;
        }
    }
}
