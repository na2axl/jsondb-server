using System;
using System.Windows;
using System.Windows.Controls;

namespace JSONDB.JQLEditor
{
    /// <summary>
    /// Interaction logic for EditConnectionWindow.xaml
    /// </summary>
    public partial class EditConnectionWindow : Window
    {
        private int _i;

        public EditConnectionWindow(Window o, int index)
        {
            // Set the owner
            Owner = o;

            // Save the index
            _i = index;

            // Initialize UI
            InitializeComponent();

            // Get the entry by index
            string entry = App.Settings.Connections[_i];

            string[] parts = System.Text.RegularExpressions.Regex.Split(entry, "\\{\\{s\\}\\}");

            ConnectionNameBox.Text = parts[0];
            ServerNameBox.Text = parts[1];
            UsernameBox.Text = parts[2];
            PasswordBox.Password = parts[3];
            DatabaseNameBox.Text = parts[4] ?? string.Empty;
        }

        private void EditConnection(object sender, RoutedEventArgs e)
        {
            App.Settings.Connections[_i] = 
                ConnectionNameBox.Text + "{{s}}" +
                ServerNameBox.Text + "{{s}}" +
                UsernameBox.Text + "{{s}}" +
                PasswordBox.Password + "{{s}}" +
                DatabaseNameBox.Text;

            App.Settings.Save();

            Close();
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
