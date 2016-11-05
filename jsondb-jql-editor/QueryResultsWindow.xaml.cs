using JSONDB.JQLEditor.TextEditor;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

            // Set the highlighter
            ResultBox.CurrentHighlighter = HighlighterManager.Instance.LoadXML(AppResources.JSONSyntax);
        }

        /// <summary>
        /// Populate the window with results.
        /// </summary>
        /// <param name="results">Results to use</param>
        public void Populate(JObject[] results)
        {
            // Update the UI
            UpdateTheme();

            // Clear the result box
            ResultBox.CleanDocument();

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
        }

        /// <summary>
        /// Update the window theme to match with the editor theme.
        /// </summary>
        public void UpdateTheme()
        {
            // Reload settings
            Settings.Reload();

            // Show/Hide line numbers
            ResultBox.IsLineNumbersMarginVisible = Settings.ShowLineNumbers;

            // Set the editor theme
            switch (Settings.EditorTheme)
            {
                case "Black":
                    ResultBox.TextEditorBackgroundColor = (Brush)(new BrushConverter().ConvertFrom("#333333"));
                    ResultBox.LineNumbersBackgroundColor = (Brush)(new BrushConverter().ConvertFrom("#252121"));
                    ResultBox.TextColor = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));
                    break;

                default:
                case "White":
                    ResultBox.TextEditorBackgroundColor = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));
                    ResultBox.LineNumbersBackgroundColor = (Brush)(new BrushConverter().ConvertFrom("#e5e5e5"));
                    ResultBox.TextColor = (Brush)(new BrushConverter().ConvertFrom("#000000"));
                    break;
            }

            // Refresh UI
            InvalidateVisual();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            QueriesList.Focus();
            base.OnGotFocus(e);
        }

        /// <summary>
        /// Hide the window instead of close it.
        /// </summary>
        /// <param name="e">The OnClosing event</param>
        protected override void OnClosing(CancelEventArgs e)
        {
            Visibility = Visibility.Collapsed;
            e.Cancel = true;
            base.OnClosing(e);
        }
    }
}
