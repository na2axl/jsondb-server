﻿using Newtonsoft.Json.Linq;
using System;
using System.Windows;
using System.Windows.Controls;

namespace JSONDB.JQLEditor
{
    /// <summary>
    /// Interaction logic for ConnectionsManager.xaml
    /// </summary>
    public partial class ConnectionsManager : Window
    {
        public ConnectionsManager(Window o)
        {
            // Set the owner
            Owner = o;

            // Initialize UI
            InitializeComponent();

            // Load Icons
            ButtonAddConnection.Source = MainWindow.BitmapToImageSource(AppResources.AddConnectionIcon);
            ButtonDeleteConnection.Source = MainWindow.BitmapToImageSource(AppResources.DeleteConnectionIcon);
            ButtonEditConnection.Source = MainWindow.BitmapToImageSource(AppResources.EditConnectionIcon);

            // Populate the list
            PopulateConnectionList();
        }

        private void PopulateConnectionList()
        {
            // If the list is empty, create a new one
            ConnectionsList.Items.Clear();
            for (int i = 0, l = App.Settings.Connections.Count; i < l; i++)
            {
                string item = App.Settings.Connections[i];
                int index = i;

                JObject info = new JObject();
                string[] parts = System.Text.RegularExpressions.Regex.Split(item, "\\{\\{s\\}\\}");

                info["name"] = parts[0];
                info["server"] = parts[1];
                info["username"] = parts[2];
                info["password"] = parts[3];
                info["database"] = parts[4] ?? string.Empty;

                ConnectionEntry entry = new ConnectionEntry(info);
                entry.Selected += (s, e) =>
                {
                    ConnectionsList.Items.MoveCurrentToPosition(index);
                };
                entry.MouseDoubleClick += (s, e) =>
                {
                    StartConnection(s, e);
                };

                ConnectionsList.Items.Add(entry);
            }
        }

        private class ConnectionEntry : ListBoxItem
        {
            private JObject _data;

            public JObject Entry
            {
                get { return _data; }
            }

            public ConnectionEntry(JObject entry)
            {
                _data = entry;
                Content = entry["name"];

                Padding = new Thickness(2, 5, 2, 5);
                Margin = new Thickness(2);
            }
        }

        private void AddConnection(object sender, RoutedEventArgs e)
        {
            AddConnectionWindow w = new AddConnectionWindow(this);
            w.ShowDialog();
            PopulateConnectionList();
        }

        private void EditConnection(object sender, RoutedEventArgs e)
        {
            EditConnectionWindow w = new EditConnectionWindow(this, ConnectionsList.Items.CurrentPosition);
            w.ShowDialog();
            PopulateConnectionList();
        }

        private void DeleteConnection(object sender, RoutedEventArgs e)
        {
            MessageWindowResult choice = new MessageWindow(
                this,
                "Are you sure you want to delete this connection?",
                "Confirm",
                MessageWindowButton.YesNo,
                MessageWindowImage.Warning).Open();

            switch (choice)
            {
                case MessageWindowResult.Yes:
                    App.Settings.Connections.RemoveAt(ConnectionsList.Items.CurrentPosition);
                    App.Settings.Save();
                    PopulateConnectionList();
                    break;
            }
        }

        private void StartConnection(object sender, RoutedEventArgs e)
        {
            JObject entry = ((ConnectionEntry)ConnectionsList.Items.CurrentItem).Entry;

            try
            {
                App.Connect(entry["server"].ToString(), entry["username"].ToString(), entry["password"].ToString(), entry["database"].ToString());
                Close();
            }
            catch (Exception ex)
            {
                new MessageWindow(
                    this,
                    ex.Message,
                    "Error",
                    MessageWindowButton.Ok,
                    MessageWindowImage.Error).Open();
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
