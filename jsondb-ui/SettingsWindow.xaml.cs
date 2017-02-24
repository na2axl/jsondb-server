using System.Windows;

namespace JSONDB.UI
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;

        public SettingsWindow(Window o)
        {
            // Set window default values
            Owner = o;
            ShowInTaskbar = false;

            // Initialize the UI
            InitializeComponent();

            // Load Application settings
            _settings = new AppSettings();

            // Set buttons states
            cancelSettingsButton.IsCancel = true;
            saveSettingsButton.IsDefault = true;

            // Update the UI
            UpdateUi();
        }

        private void UpdateUi()
        {
            UseCustomServerAddress.IsChecked = _settings.UseCustomServerAdress;
            CustomServerAddress.IsEnabled = _settings.UseCustomServerAdress;
            CustomServerAddress.Text = _settings.CustomServerAdress;
        }

        private void CustomServerCheckboxChecked(object sender, RoutedEventArgs e)
        {
            CustomServerAddress.IsEnabled = true;
            TestServerAddressButton.IsEnabled = true;
        }

        private void CustomServerCheckboxUnchecked(object sender, RoutedEventArgs e)
        {
            CustomServerAddress.IsEnabled = false;
            TestServerAddressButton.IsEnabled = false;
        }

        private void TestServerAddress(object sender, RoutedEventArgs e)
        {
            var msgWaitBox = new MessageWindow(this, "Testing server address... Please wait...", "Testing Server Address", MessageWindowButton.None, MessageWindowImage.Information);
            msgWaitBox.Show();
            if (Util.TestServerAddress(CustomServerAddress.Text))
            {
                msgWaitBox.Close();
                new MessageWindow(this, "The server address works fine !", "Testing Server Address", MessageWindowButton.Ok, MessageWindowImage.Success).Open();
            }
            else
            {
                msgWaitBox.Close();
                new MessageWindow(this, "The server address is not working !", "Testing Server Address", MessageWindowButton.Ok, MessageWindowImage.Error).Open();
            }
        }

        private void SaveSettings(object sender, RoutedEventArgs e)
        {
            _settings.UseCustomServerAdress = (bool)UseCustomServerAddress.IsChecked;
            _settings.CustomServerAdress = (string)CustomServerAddress.Text;
            _settings.JDBTAssociation = (bool)AssociateJDBTFiles.IsChecked;
            _settings.JQLAssociation = (bool)AssociateJQLFiles.IsChecked;
            _settings.Save();

            if (_settings.JDBTAssociation)
            {
                App.RunElevatedClient("--set-association .jdbt JSONDB_Table_File \"" + Util.MakePath(Util.AppRoot(), "jsondb-jql-editor.exe") + "\" \"JSONDB Table\"");
            }

            if (_settings.JQLAssociation)
            {
                App.RunElevatedClient("--set-association .jql JQL_File \"" + Util.MakePath(Util.AppRoot(), "jsondb-jql-editor.exe") + "\" \"JQL File\"");
            }

            new MessageWindow(this, "Settings Saved.", Title, MessageWindowButton.Ok, MessageWindowImage.Success).Open();
        }

        private void DefaultSettings(object sender, RoutedEventArgs e)
        {
            _settings.Reset();
            UpdateUi();
        }

        private void CancelSettings(object sender, RoutedEventArgs e)
        {
            _settings.Reload();
            Close();
        }
    }
}
