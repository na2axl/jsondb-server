using JSONDB.Library;
using System;
using System.Windows;
using System.Windows.Controls;

namespace JSONDB.JQLEditor
{
    /// <summary>
    /// Interaction logic for AddConnectionWindow.xaml
    /// </summary>
    public partial class AddConnectionWindow : Window
    {
        public AddConnectionWindow(Window o)
        {
            // Set the owner
            Owner = o;

            // Initialize UI
            InitializeComponent();
        }

        private void AddConnection(object sender, RoutedEventArgs e)
        {
            try
            {
                Database temp_db = Library.JSONDB.Connect(ServerNameBox.Text, UsernameBox.Text, PasswordBox.Password, DatabaseNameBox.Text);
                if (temp_db.IsConnected())
                {
                    App.Settings.Connections.Add(
                        ConnectionNameBox.Text + "{{s}}" +
                        ServerNameBox.Text + "{{s}}" +
                        UsernameBox.Text + "{{s}}" +
                        PasswordBox.Password + "{{s}}" + 
                        DatabaseNameBox.Text);

                    App.Settings.Save();

                    new MessageWindow(
                        this,
                        "Connection added successfully.",
                        "Connection added",
                        MessageWindowButton.OK,
                        MessageWindowImage.Success).Open();

                    temp_db.Disconnect();

                    Close();
                }
                else
                {
                    throw new Exception("An error occurred when trying to connect to this server.");
                }
            }
            catch (Exception ex)
            {
                new MessageWindow(
                    this,
                    ex.Message,
                    "Error",
                    MessageWindowButton.OK,
                    MessageWindowImage.Error).Open();
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
