using System.Windows;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using TrimuiSmartHub.Application.Services.Trimui;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using TrimuiSmartHub.Application.Repository;
using CsQuery.ExtensionMethods.Internal;
using TrimuiSmartHub.Application.Services.LibRetro;
using TrimuiSmartHub.Application.Helpers;
using System.Windows.Media.Effects;

namespace TrimuiSmartHub.Application.Frames
{
    public partial class GameImageScrapper : Page
    {
        private ProgressDialogController Controller { get; set; }
        public GameImageScrapper()
        {
            InitializeComponent();

            PopulateEmulators();

        }

        private void PopulateEmulators()
        {
            Task.Run(() =>
            {
                var emulatorList = TrimuiService.New().GetEmulators();

                foreach (var emulator in emulatorList)
                {
                    var emulatorRoms = TrimuiService.New().GetRomsByEmulator(emulator);

                    Dispatcher.Invoke(() =>
                    {
                        var emulatorCard = CreateGameComponent(emulator, emulatorRoms);

                        if (emulatorCard != null)
                        {
                            Container.Children.Add(emulatorCard);
                        }
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    Panel.Visibility = Visibility.Visible;
                    LoadingIndicator.Visibility = Visibility.Collapsed;
                });
            });
        }
        private Button? CreateGameComponent(string emulator, List<string> romsList)
        {
            string emulatorDescription;

            EmulatorDictionary.EmulatorMap.TryGetValue(emulator, out emulatorDescription);

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
                BorderThickness = new Thickness(0),
            };

            button.Click += async (sender, e) =>
            {
                var metroWindow = (MetroWindow)System.Windows.Application.Current.MainWindow;

                var imgFolder = TrimuiService.New().GetImageFolder(emulator);
                var count = 0;
                var totalRoms = romsList.Count;

                int processedRoms = 0;

                var libRetro = LibretroService.New();

                await Loading(true, $"Downloading games box art...");

                await Task.Run(async () =>
                {
                    foreach (var romFile in romsList)
                    {
                        var rom = romFile.Split('.').First();

                        if (File.Exists(Path.Combine(imgFolder, $"{rom}.png")))
                        {
                            Interlocked.Increment(ref processedRoms);

                            continue;
                        }

                        var boxImage = await libRetro.SearchThumbnail(emulatorDescription, romFile);

                        if (boxImage == null)
                        {
                            continue;
                        }

                        try
                        {
                            File.WriteAllBytes(Path.Combine(imgFolder, $"{rom}.png"), boxImage);
                            Interlocked.Increment(ref count);
                        }
                        catch (Exception ex)
                        {
                            //ignore
                        }
                    }
                   
                });


                await Loading(false);

                if (count > 0) await ShowMessageAsync("Download Completed!", $"{count} Files was updated!");
            };

            return button;
        }
        private async Task<MessageDialogResult> ShowMessageAsync(string title, string content, MetroDialogSettings dialogSettings = null)
        {
            if (dialogSettings == null)
            {
                dialogSettings = new MetroDialogSettings
                {
                    AffirmativeButtonText = "OK", 
                    NegativeButtonText = "Cancel", 
                    AnimateShow = true,
                    AnimateHide = true,
                    ColorScheme = MetroDialogColorScheme.Inverted,
                    DialogTitleFontSize = 30,
                    DialogMessageFontSize = 20,
                    DefaultButtonFocus = MessageDialogResult.Affirmative,
                };
            }

            var metroWindow = (MetroWindow)System.Windows.Application.Current.MainWindow;

            return await metroWindow.ShowMessageAsync(title, content, MessageDialogStyle.Affirmative, dialogSettings);
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

                Controller.SetIndeterminate();

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

        private async Task UpdateProgressAsync(int processedRoms, int totalRoms)
        {
            double progress = (double)processedRoms / totalRoms; 
            await Dispatcher.InvokeAsync(() => Controller.SetProgress(progress)); 
        }
    }
}
