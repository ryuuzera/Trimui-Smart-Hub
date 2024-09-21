using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using TrimuiSmartHub.Application.Model;
using System.Windows.Data;
using System.Globalization;
using TrimuiSmartHub.Application.Services;
using System.Windows.Input;
using TrimuiSmartHub.Application.Repository;
using TrimuiSmartHub.Application.Helpers;
using System.Diagnostics;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.Controls;
using System.Runtime.CompilerServices;

namespace TrimuiSmartHub.Application.Frames
{
    public class WidthAdjustmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                // Subtrair 10 pixels da largura original
                return width - 10;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public partial class GameDownloaderSearch : Page
    {
        private ProgressDialogController Controller { get; set; }
        private List<GameInfoRetrostic> GameList { get; set; }
        private string Emulator { get; set; }
        public GameDownloaderSearch(string emulator)
        {
            InitializeComponent();

            string emulatorDescription;

            EmulatorDictionary.EmulatorMapSystem.TryGetValue(emulator, out emulatorDescription);

            if (emulatorDescription.IsNotNullOrEmpty()) ConsoleName.Text = $"{emulatorDescription} Games";

            Emulator = emulator;

            Task.Run(() => PopulateGameList(emulator));
        }

        private void PopulateGameList(string emulator)
        {
            Task.Run(async () =>
            {
                var gameList = await RetrosticService.New().ListGamesByEmulator(emulator);

                if (gameList == null) return;

                GameList = gameList;

                foreach (var item in GameList)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var button = CreateGameButton(item);

                        Container.Children.Add(button);
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    LoadingComponent.Visibility = Visibility.Collapsed;
                    Container.Visibility = Visibility.Visible;
                });
            });
        }

        public Button CreateGameButton(GameInfoRetrostic gameInfo)
        {
            Button button = new Button
            {
                Margin = new Thickness(0, 10, 0, 0),
                Style = (Style)System.Windows.Application.Current.FindResource("TransparentButton"),
                Cursor = Cursors.Hand
            };

            Border border = new Border
            {
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAB8C2")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5), 
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B31C2E41")),
                Padding = new Thickness(10)
            };

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });


            try
            {
                var imgSrc = new BitmapImage(gameInfo.BoxArt);
                Image image = new Image
                {
                    Source = imgSrc,
                    Width = 100,
                    Height = 70,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(image, 0);

                grid.Children.Add(image);
            }
            catch (Exception)
            {
                //
            }

            TextBlock textBlock = new TextBlock
            {
                Text = gameInfo.Title,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAB8C2")),
                FontWeight = FontWeights.SemiBold,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(50, 0, 0, 0)
            };
            Grid.SetColumn(textBlock, 1);

            grid.Children.Add(textBlock);

            TextBlock textRegion = new TextBlock
            {
                Text = gameInfo.Region,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAB8C2")),
                FontWeight = FontWeights.SemiBold,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(50, 0, 0, 0)
            };
            Grid.SetColumn(textRegion, 2);

            grid.Children.Add(textRegion);

            Binding binding = new Binding("ActualWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(WrapPanel), 1),
                Converter = new WidthAdjustmentConverter() 
            };
            border.SetBinding(Border.WidthProperty, binding);

            border.Child = grid;

            button.Content = border;

            button.Click += async (sender, e) =>
            {
                await Loading(true, $"Downloading {gameInfo.Title}");
  
                await Task.Run(async () =>
                {
                    var (contentStream, fileStream, totalBytes) = await RetrosticService.New().DownloadGame(gameInfo);

                    var buffer = new byte[8192];
                    long totalBytesRead = 0;
                    int bytesRead;
                    var stopwatch = Stopwatch.StartNew();

                    Controller.Maximum = 100;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;

                        double elapsedTime = stopwatch.Elapsed.TotalSeconds;

                        double downloadSpeed = totalBytesRead / elapsedTime; 

                        double downloadSpeedMbps = downloadSpeed / (1024 * 1024);
           
                        double downloadSpeedKbps = downloadSpeed / 1024;

                        Debug.WriteLine($"Downloaded {(double)totalBytesRead * 100 / totalBytes:0.00}%.");

                        double progress = (totalBytesRead * 100 / totalBytes);

                        progress = Math.Min(progress, 100);

                        await Dispatcher.InvokeAsync(() => {
                            Controller.SetProgress(progress);
                            Controller.SetMessage($"Progress: {progress:0.00}% | Download Speed: {downloadSpeedMbps:0.00} MB/s");
                        });
                    }

                    fileStream.Close();
                    contentStream.Close();
                });

                await Loading(false);
            };


            return button;
        }

        private async Task Loading(bool isLoading, string controllerTitle = null, Action cancelAction = null)
        {
            try
            {
                await CloseController();

                if (!isLoading) return;

                var metroDialogSettings = new MetroDialogSettings
                {
                    DialogTitleFontSize = 30,
                    DialogMessageFontSize = 20,
                    AnimateShow = true,
                    AnimateHide = true,
                    ColorScheme = MetroDialogColorScheme.Inverted
                };

                var metroWindow = (MetroWindow)System.Windows.Application.Current.MainWindow;

                controllerTitle = controllerTitle.IsNullOrEmpty() ? "Downloading box arts..." : controllerTitle;

                Controller = await metroWindow.ShowProgressAsync(controllerTitle, "Please don't disconnect the device.", cancelAction != null, metroDialogSettings);

                //Controller.SetIndeterminate();

                Controller.Canceled += (sender, args) =>
                {
                    cancelAction?.Invoke();
                };
            }
            catch (Exception)
            {
                await CloseController();
            }
        }

        private async Task CloseController()
        {
            try
            {
                if (Controller != null)
                {
                    if (Controller.IsOpen) await Controller.CloseAsync();

                    Controller = null;
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void Search_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Container.Children.Clear();

                LoadingComponent.Visibility = Visibility.Visible;

                Task.Run(async () =>
                {
                    var searchTerm = Dispatcher.Invoke(() => SearchTermTextBox.Text);
                    var gameList = await RetrosticService.New().SearchGamesByName(searchTerm, Emulator);

                    if (gameList == null) return;

                    GameList = gameList;

                    foreach (var item in GameList)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var button = CreateGameButton(item);

                            Container.Children.Add(button);
                        });
                    }

                    Dispatcher.Invoke(() =>
                    {
                        LoadingComponent.Visibility = Visibility.Collapsed;
                        Container.Visibility = Visibility.Visible;
                    });
                });
            }
            catch (Exception)
            {
               //
            }
        }
    }
}
