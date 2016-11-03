using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
    /// Interaction logic for QueryResultsWindow.xaml
    /// </summary>
    public partial class QueryResultsWindow : Window
    {
        // Application Settings
        private AppSettings Settings;

        public QueryResultsWindow(Window o)
        {
            // Set the owner
            Owner = o;

            // Load application settings
            Settings = new AppSettings();

            // Initialize the UI
            InitializeComponent();
        }

        public void Populate(JObject[] results)
        {
            // Initialize the list of queries
            QueriesList.Items.Clear();
            for (int i = 0; i < results.Length; i++)
            {
                int current = i;
                ComboBoxItem item = new ComboBoxItem();
                item.Content = "#" + (i + 1);
                item.Selected += (s, e) =>
                {
                    ((MainWindow)Owner).SelectQueryBlock(current);
                    ResultBox.SetDocumentContents(results[current].ToString());
                };

                QueriesList.Items.Add(item);
            }

            // Select first item by default
            QueriesList.Items.MoveCurrentToFirst();
            ((ComboBoxItem)QueriesList.Items.CurrentItem).IsSelected = true;
        }

        public void UpdateTheme()
        {
            // Show/Hide line numbers
            ResultBox.IsLineNumbersMarginVisible = Settings.ShowLineNumbers;

            // Set the editor theme
            switch (Settings.EditorTheme)
            {
                case "Black":
                    ResultBox.Foreground = (Brush)(new BrushConverter().ConvertFrom("#333333"));
                    ResultBox.Background = (Brush)(new BrushConverter().ConvertFrom("#252121"));
                    ResultBox.TextColor = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));
                    break;

                default:
                case "White":
                    ResultBox.Foreground = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));
                    ResultBox.Background = (Brush)(new BrushConverter().ConvertFrom("#e5e5e5"));
                    ResultBox.TextColor = (Brush)(new BrushConverter().ConvertFrom("#000000"));
                    break;
            }
        }
    }
}
