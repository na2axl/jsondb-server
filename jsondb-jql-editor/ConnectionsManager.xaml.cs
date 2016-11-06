using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace JSONDB.JQLEditor
{
    /// <summary>
    /// Interaction logic for ConnectionsManager.xaml
    /// </summary>
    public partial class ConnectionsManager : Window
    {
        private AppSettings Settings;

        public ConnectionsManager(Window o)
        {
            // Set the owner
            Owner = o;

            // Load Application Settings
            Settings = new AppSettings();

            // Initialize UI
            InitializeComponent();

            // Load Icons
            ButtonAddConnection.Source = BitmapToImageSource(AppResources.AddConnectionIcon);
            ButtonDeleteConnection.Source = BitmapToImageSource(AppResources.DeleteConnectionIcon);
            ButtonEditConnection.Source = BitmapToImageSource(AppResources.EditConnectionIcon);

            // Load connections list
            ConnectionsList.Items.Clear();
            foreach (JObject item in Settings.Connections)
            {
                ConnectionEntry entry = new ConnectionEntry(item);
            }
        }

        private ImageSource BitmapToImageSource(System.Drawing.Bitmap bmp)
        {
            MemoryStream memory = new MemoryStream();
            BitmapImage imageSource = new BitmapImage();

            bmp.Save(memory, AppResources.MessageWindowError.RawFormat);
            memory.Position = 0;
            imageSource = new BitmapImage();
            imageSource.BeginInit();
            imageSource.StreamSource = memory;
            imageSource.CacheOption = BitmapCacheOption.OnLoad;
            imageSource.EndInit();
            memory.Close();

            return imageSource;
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
            }
        }

        private void AddConnection(object sender, RoutedEventArgs e)
        {

        }
    }
}
