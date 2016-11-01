using JSONDB.JQLEditor.TextEditor;
using JSONDB.Library;
using Newtonsoft.Json.Linq;
using System;
using System.Windows;
using System.Windows.Media;

namespace JSONDB.JQLEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            TextEditor.CurrentHighlighter = HighlighterManager.Instance.Highlighters["JQL"];
            if (App.CurrentWorkingFile != String.Empty)
            {
                TextEditor.Loaded += (s, e) =>
                {
                    TextEditor.Text = Util.ReadTextFile(App.CurrentWorkingFile);
                };
            }
            TextEditor.TextChanged += (s, e) =>
            {
                ValidateQueries(s, e);
            };
            TextEditor.Focus();
        }

        private void ValidateQueries(object sender, RoutedEventArgs e)
        {
            if (TextEditor.Text.Length > 0)
            {
                try
                {
                    SetStatus("Validating query...");
                    JObject[] test = QueryParser.MultilineParse(TextEditor.Text);
                    StatusMessage.Background = (Brush)(new BrushConverter().ConvertFrom("#0066CC"));
                    StatusMessage.Foreground = Brushes.White;
                    SetStatus("No Errors");
                }
                catch (MultilineQueryParseException err)
                {
                    //TextEditor.SelectQueryBlock(err.Line - 1);
                    StatusMessage.Background = (Brush)(new BrushConverter().ConvertFrom("#CC0000"));
                    StatusMessage.Foreground = Brushes.White;
                    SetStatus(err.ToString());
                }
                catch (Exception err)
                {
                    StatusMessage.Background = (Brush)(new BrushConverter().ConvertFrom("#CC0000"));
                    StatusMessage.Foreground = Brushes.White;
                    SetStatus(err.Message);
                }
            }
            else
            {
                SetStatus("No Query.");
            }
        }

        private void SetStatus(string message)
        {
            StatusMessage.Content = message;
        }
    }
}
