using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
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

namespace JSONDB.UI
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow(Window o)
        {
            // Set window default values
            Owner = o;
            ShowInTaskbar = false;
            
            // Initialize the UI
            InitializeComponent();

            // Show the logo
            MemoryStream memory = new MemoryStream();
            AppResources.ProgramLogo.Save(memory, ImageFormat.Png);
            memory.Position = 0;
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            JSONDBLogo.Source = bitmapImage;

            // Set the version
            VersionText.Content = AppResources.AppVersion;

            // Set the copyright
            CopyrightText.Content = AppResources.AppCopyright;

            // Show the License
            LicenseText.Text = AppResources.LICENSE;
        }
    }
}
