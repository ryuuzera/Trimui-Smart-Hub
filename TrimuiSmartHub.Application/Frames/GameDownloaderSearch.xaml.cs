using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using TrimuiSmartHub.Application.Model;
using System.Windows.Data;
using System.Globalization;

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
        private List<GameInfoRetrostic> GameList { get; set; }
        public GameDownloaderSearch(List<GameInfoRetrostic> gameList)
        {
            InitializeComponent();

            GameList = gameList;

            PopulateGameList();

        }

        private void PopulateGameList()
        {
            foreach (var item in GameList)
            {
                var button = CreateGameButton(item.BoxArt, item.Title);

                Container.Children.Add(button);
            }
        }

        public Button CreateGameButton(Uri imageUrl, string gameTitle)
        {
            Button button = new Button
            {
                Margin = new Thickness(0, 10, 0, 0),
                Style = (Style)System.Windows.Application.Current.FindResource("TransparentButton")
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            try
            {
                var imgSrc = new BitmapImage(imageUrl);
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
                Text = gameTitle.Replace("Roms", string.Empty),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AAB8C2")),
                FontWeight = FontWeights.SemiBold,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(50, 0, 0, 0)
            };
            Grid.SetColumn(textBlock, 1);

         
            grid.Children.Add(textBlock);

            Binding binding = new Binding("ActualWidth")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(WrapPanel), 1),
                Converter = new WidthAdjustmentConverter() // Aplicar o conversor que diminui a largura
            };
            border.SetBinding(Border.WidthProperty, binding);

            border.Child = grid;

            button.Content = border;

            return button;
        }

    }
}
