using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrimuiSmartHub.Application.Repository;
using TrimuiSmartHub.Application.Services.LibRetro;
using TrimuiSmartHub.Application.Services.Trimui;
using TrimuiSmartHub.Application.Helpers;
using TrimuiSmartHub.Application.Services;
using CsQuery.Implementation;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.Controls;
using System.Linq;
using TrimuiSmartHub.Application.Pages;
using CsQuery.Engine.PseudoClassSelectors;

namespace TrimuiSmartHub.Application.Frames
{
    public partial class GameDownloader : Page
    {   
        private SmartProHub Parent {  get; set; } 
        public GameDownloader(SmartProHub parent)
        {   
            InitializeComponent();

            Parent = parent;

            PopulateEmulators();
        }

        private void PopulateEmulators()
        {
            Task.Run(() =>
            {
                var emulatorList = TrimuiService.New().GetEmulators();

                foreach (var emulator in emulatorList)
                {

                    Dispatcher.Invoke(() =>
                    {
                        var emulatorCard = CreateGameComponent(emulator);

                        if (emulatorCard != null)
                        {
                            RomsContainer.Children.Add(emulatorCard);
                        }
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    RomsPanel.Visibility = Visibility.Visible;
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                });
            });
        }
        private Button? CreateGameComponent(string emulator)
        {
            string emulatorDescription;

            EmulatorDictionary.EmulatorMapRetrostic.TryGetValue(emulator, out emulatorDescription);

            if (emulatorDescription.IsNullOrEmpty()) return null;

            Border border = new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1),
                    GradientStops = new GradientStopCollection
                    {
                            new GradientStop(Color.FromArgb(35, 25, 84, 112), 1),
                            new GradientStop(Color.FromArgb(75, 45, 84, 112), 1)
                    }
                },
                CornerRadius = new CornerRadius(10),
                Width = 120,
                Height = 120,
                Margin = new Thickness(5),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(75, 45, 84, 112)),
            };

            StackPanel stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            try
            {
                var imgSource = new BitmapImage(new Uri($"pack://application:,,,/Resources/Images/Emulators/{emulator}.png"));
                Image image = new Image
                {
                    Source = imgSource,
                    Width = 70,
                    Height = 60,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 0)
                };

                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

                stackPanel.Children.Add(image);
            }
            catch (Exception)
            {
                //
            }
            

            TextBlock titleBlock = new TextBlock
            {
                Text = emulator,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(titleBlock);

            border.Child = stackPanel;

            Button button = new Button
            {
                Content = border,
                Style = (Style)System.Windows.Application.Current.FindResource("GameButtonStyle"),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            button.Click += async (sender, e) =>
            {
                //await Loading(true, $"Downloading games boxart...");

                var count = 0;

                Task.Run(async () =>
                {
                    //var download = RetrosticService.New().DownloadGame(emulator, "Street Fighter");

                    Dispatcher.Invoke(() =>
                    {
                        Parent.NavigationFrame.Navigate(new GameDownloaderSearch(emulator));
                    });

                });

                //await Loading(false);

                //await ShowMessageAsync("Download Completed!", (count > 0) ? $"{count} Files was updated!" : "The boxarts already updated!");
            };

            return button;
        }
    }
}
