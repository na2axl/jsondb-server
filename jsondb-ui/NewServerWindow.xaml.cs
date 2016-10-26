using System.Windows;

namespace JSONDB.UI
{
    /// <summary>
    /// Interaction logic for NewServerWindow.xaml
    /// </summary>
    public partial class NewServerWindow : Window
    {
        public NewServerWindow(Window o)
        {
            // Set window default values
            Owner = o;
            ShowInTaskbar = false;

            // Initialize the UI
            InitializeComponent();

            // Set buttons states
            CreateButton.IsDefault = true;
            CancelButton.IsCancel = true;
        }

        private void LockUI()
        {
            ServerNameBox.IsEnabled = false;
            UsernameBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            CreateButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
        }

        private void UnlockUI()
        {
            ServerNameBox.IsEnabled = true;
            UsernameBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;
            CreateButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }

        private void CreateServer(object sender, RoutedEventArgs e)
        {
            LockUI();
            if (JSONDB.ServerExists(ServerNameBox.Text))
            {
                new MessageWindow(this, "A server with this name already exist.", Title, MessageWindowButton.OK, MessageWindowImage.Error).Open();
            }
            else
            {
                JSONDB.CreateServer(ServerNameBox.Text, UsernameBox.Text, PasswordBox.Text);
                new MessageWindow(this, "The server is successfully created.", Title, MessageWindowButton.OK, MessageWindowImage.Success).Open();
                Close();
            }
            UnlockUI();
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
