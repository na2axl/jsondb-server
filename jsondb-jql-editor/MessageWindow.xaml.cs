using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JSONDB.JQLEditor
{
    public enum MessageWindowButton
    {
        None = 0,
        Ok = 1,
        OkCancel = 2,
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
        Ok = 9,
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
        private MessageWindowResult ClickedButton { get; set; } = MessageWindowResult.None;
        private SystemSound SoundToPlay { get; set; }

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
            MessageBoxText.Text = message;

            Bitmap imageToShow = null;

            // Set the image to show and the sound to play
            switch (image)
            {
                case MessageWindowImage.Information:
                    imageToShow = AppResources.MessageWindowInformation;
                    SoundToPlay = SystemSounds.Asterisk;
                    break;
                case MessageWindowImage.Warning:
                    imageToShow = AppResources.MessageWindowWarning;
                    SoundToPlay = SystemSounds.Exclamation;
                    break;
                case MessageWindowImage.Error:
                    imageToShow = AppResources.MessageWindowError;
                    SoundToPlay = SystemSounds.Hand;
                    break;
                case MessageWindowImage.Success:
                    imageToShow = AppResources.MessageWindowSuccess;
                    SoundToPlay = SystemSounds.Beep;
                    break;
                default:
                    break;
            }

            if (imageToShow != null)
            {
                var memory = new MemoryStream();
                imageToShow.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
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
                case MessageWindowButton.Ok:
                    ButtonYes.Visibility = Visibility.Collapsed;
                    ButtonCancel.Visibility = Visibility.Collapsed;
                    ButtonNo.Visibility = Visibility.Collapsed;
                    break;

                case MessageWindowButton.OkCancel:
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
                ClickedButton = MessageWindowResult.Ok;
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
            SoundToPlay.Play();
            ShowDialog();
            return ClickedButton;
        }
    }
}
