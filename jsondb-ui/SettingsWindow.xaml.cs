using JSONDB.Library;
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
            UseCustomServerAddress.IsChecked = Settings.UseCustomServerAdress;
            CustomServerAddress.IsEnabled = Settings.UseCustomServerAdress;
            CustomServerAddress.Text = Settings.CustomServerAdress;
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
            Settings.UseCustomServerAdress = (bool)UseCustomServerAddress.IsChecked;
            Settings.CustomServerAdress = (string)CustomServerAddress.Text;
            Settings.JDBTAssociation = (bool)AssociateJDBTFiles.IsChecked;
            Settings.JQLAssociation = (bool)AssociateJQLFiles.IsChecked;
            Settings.Save();

            if (Settings.JDBTAssociation)
            {
                App.RunElevatedClient("--set-association .jdbt JSONDB_Table_File \"" + Util.MakePath(Util.AppRoot(), "jsondb-jql-editor.exe") + "\" \"JSONDB Table\"");
            }

            if (Settings.JQLAssociation)
            {
                App.RunElevatedClient("--set-association .jql JQL_File \"" + Util.MakePath(Util.AppRoot(), "jsondb-jql-editor.exe") + "\" \"JQL File\"");
            }

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
