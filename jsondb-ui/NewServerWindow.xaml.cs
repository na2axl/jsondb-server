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

        private void LockUi()
        {
            ServerNameBox.IsEnabled = false;
            UsernameBox.IsEnabled = false;
            PasswordBox.IsEnabled = false;
            CreateButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
        }

        private void UnlockUi()
        {
            ServerNameBox.IsEnabled = true;
            UsernameBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;
            CreateButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }

        private void CreateServer(object sender, RoutedEventArgs e)
        {
            LockUi();
            if (Jsondb.ServerExists(ServerNameBox.Text))
            {
                new MessageWindow(this, "A server with this name already exist.", Title, MessageWindowButton.Ok, MessageWindowImage.Error).Open();
            }
            else
            {
                Jsondb.CreateServer(ServerNameBox.Text, UsernameBox.Text, PasswordBox.Text);
                new MessageWindow(this, "The server is successfully created.", Title, MessageWindowButton.Ok, MessageWindowImage.Success).Open();
                Close();
            }
            UnlockUi();
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
