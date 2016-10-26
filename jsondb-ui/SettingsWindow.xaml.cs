using System.Windows;

namespace JSONDB.UI
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private AppSettings Settings;

        public SettingsWindow(Window o)
        {
            // Set window default values
            Owner = o;
            ShowInTaskbar = false;

            // Initialize the UI
            InitializeComponent();

            // Load Application settings
            Settings = new AppSettings();

            // Set buttons states
            cancelSettingsButton.IsCancel = true;
            saveSettingsButton.IsDefault = true;

            // Update the UI
            UpdateUI();
        }

        private void UpdateUI()
        {
            UseCustomServerAdress.IsChecked = Settings.UseCustomServerAdress;
            CustomServerAdress.IsEnabled = Settings.UseCustomServerAdress;
            CustomServerAdress.Text = Settings.CustomServerAdress;
        }

        private void CustomServerCheckboxChecked(object sender, RoutedEventArgs e)
        {
            CustomServerAdress.IsEnabled = true;
            TestServerAddressButton.IsEnabled = true;
        }

        private void CustomServerCheckboxUnchecked(object sender, RoutedEventArgs e)
        {
            CustomServerAdress.IsEnabled = false;
            TestServerAddressButton.IsEnabled = false;
        }

        private void TestServerAddress(object sender, RoutedEventArgs e)
        {
            var msgWaitBox = new MessageWindow(this, "Testing server address... Please wait...", "Testing Server Address", MessageWindowButton.None, MessageWindowImage.Information);
            msgWaitBox.Show();
            if (Util.TestServerAddress(CustomServerAdress.Text))
            {
                msgWaitBox.Close();
                new MessageWindow(this, "The server address works fine !", "Testing Server Address", MessageWindowButton.OK, MessageWindowImage.Success).Open();
            }
            else
            {
                msgWaitBox.Close();
                new MessageWindow(this, "The server address is not working !", "Testing Server Address", MessageWindowButton.OK, MessageWindowImage.Error).Open();
            }
        }

        private void SaveSettings(object sender, RoutedEventArgs e)
        {
            Settings.UseCustomServerAdress = (bool)UseCustomServerAdress.IsChecked;
            Settings.CustomServerAdress = (string)CustomServerAdress.Text;
            Settings.Save();
            new MessageWindow(this, "Settings Saved.", Title, MessageWindowButton.OK, MessageWindowImage.Success).Open();
        }

        private void DefaultSettings(object sender, RoutedEventArgs e)
        {
            Settings.Reset();
            UpdateUI();
        }

        private void CancelSettings(object sender, RoutedEventArgs e)
        {
            Settings.Reload();
            Close();
        }
    }
}
