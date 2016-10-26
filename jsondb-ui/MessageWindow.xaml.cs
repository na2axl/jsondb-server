using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace JSONDB.UI
{

    public enum MessageWindowButton
    {
        None = 0,
        OK = 1,
        OKCancel = 2,
        YesNo = 3,
        YesNoCancel = 4
    }

    public enum MessageWindowImage
    {
        Information = 5,
        Warning = 6,
        Error = 7,
        Success = 8
    }

    public enum MessageWindowResult
    {
        OK = 9,
        Cancel = 10,
        Yes = 11,
        No = 12,
        None = -1
    }
    
    /// <summary>
    /// Interaction logic for MessageWindow.xaml
    /// </summary>
    public partial class MessageWindow : Window
    {
        private MessageWindowResult ClickedButton;

        public MessageWindow(Window o, string message, string title, MessageWindowButton buttons, MessageWindowImage image)
        {
            // Set window default values
            Owner = o;
            ShowInTaskbar = false;

            // Initialize the UI
            InitializeComponent();

            // Set buttons states
            ButtonCancel.IsCancel = true;
            ButtonOK.IsDefault = true;
            ButtonYes.IsDefault = true;

            // Set the window title
            Title = title;

            // Set the message text
            MessageBoxText.Content = message;

            Bitmap Img = null;

            // Set which image to show
            switch (image)
            {
                case MessageWindowImage.Information:
                    Img = AppResources.MessageWindowInformation;
                    break;
                case MessageWindowImage.Warning:
                    Img = AppResources.MessageWindowWarning;
                    break;
                case MessageWindowImage.Error:
                    Img = AppResources.MessageWindowError;
                    break;
                case MessageWindowImage.Success:
                    Img = AppResources.MessageWindowSuccess;
                    break;
                default:
                    break;
            }

            if (Img != null)
            {
                MemoryStream memory = new MemoryStream();
                Img.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                MessageBoxImage.Source = bitmapImage;
            }

            // Set which buttons to show
            switch (buttons)
            {
                default:
                case MessageWindowButton.OK:
                    ButtonYes.Visibility = Visibility.Collapsed;
                    ButtonCancel.Visibility = Visibility.Collapsed;
                    ButtonNo.Visibility = Visibility.Collapsed;
                    break;

                case MessageWindowButton.OKCancel:
                    ButtonYes.Visibility = Visibility.Collapsed;
                    ButtonNo.Visibility = Visibility.Collapsed;
                    break;

                case MessageWindowButton.YesNo:
                    ButtonOK.Visibility = Visibility.Collapsed;
                    ButtonCancel.Visibility = Visibility.Collapsed;
                    break;

                case MessageWindowButton.YesNoCancel:
                    ButtonOK.Visibility = Visibility.Collapsed;
                    break;

                case MessageWindowButton.None:
                    ButtonOK.Visibility = Visibility.Collapsed;
                    ButtonYes.Visibility = Visibility.Collapsed;
                    ButtonCancel.Visibility = Visibility.Collapsed;
                    ButtonNo.Visibility = Visibility.Collapsed;
                    break;
            }

            ButtonCancel.Click += (object sender, RoutedEventArgs e) =>
            {
                ClickedButton = MessageWindowResult.Cancel;
                Close();
            };

            ButtonNo.Click += (object sender, RoutedEventArgs e) =>
            {
                ClickedButton = MessageWindowResult.No;
                Close();
            };

            ButtonOK.Click += (object sender, RoutedEventArgs e) =>
            {
                ClickedButton = MessageWindowResult.OK;
                Close();
            };

            ButtonYes.Click += (object sender, RoutedEventArgs e) =>
            {
                ClickedButton = MessageWindowResult.Yes;
                Close();
            };
        }

        public MessageWindowResult Open()
        {
            ShowDialog();
            return ClickedButton;
        }
    }
}
