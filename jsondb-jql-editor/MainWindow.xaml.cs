using JSONDB.JQLEditor.TextEditor;
using JSONDB.Library;
using System;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Input;

namespace JSONDB.JQLEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        enum StatusMessageState
        {
            Information,
            Error,
            Loading
        }

        private bool changesSaved = true;

        public MainWindow()
        {
            // Initialize the UI
            InitializeComponent();

            // Set the syntax highighter
            TextEditor.CurrentHighlighter = HighlighterManager.Instance.Highlighters["JQL"];

            // If we have to open a file
            if (App.CurrentWorkingFile != String.Empty)
            {
                TextEditor.Loaded += (s, e) =>
                {
                    OpenDocumentAt(App.CurrentWorkingFile);
                };
            }

            // Custom events
            TextEditor.TextChanged += (s, e) =>
            {
                // Syntax checker
                QueryValidator();

                // Update status bar
                StatusEditorInfo.Content = TextEditor.ToString();

                // Invalidate the save state
                changesSaved = false;
            };

            TextEditor.PreviewKeyDown += (s, e) =>
            {
                // Update status bar
                StatusEditorInfo.Content = TextEditor.ToString();
            };

            TextEditor.PreviewKeyUp += (s, e) =>
            {
                // Update status bar
                StatusEditorInfo.Content = TextEditor.ToString();
            };

            TextEditor.PreviewMouseDown += (s, e) =>
            {
                // Update status bar
                StatusEditorInfo.Content = TextEditor.ToString();
            };

            TextEditor.PreviewMouseUp += (s, e) =>
            {
                // Update status bar
                StatusEditorInfo.Content = TextEditor.ToString();
            };

            // Focus on the editor
            TextEditor.Focus();
        }

        /// <summary>
        /// Save the current document at the given path.
        /// </summary>
        /// <param name="path">The path where to save the document.</param>
        public void SaveDocument(string path)
        {
            if (!path.EndsWith(".jql"))
            {
                path = path + ".jql";
            }
            Util.WriteTextFile(path, TextEditor.GetDocumentContents());
            changesSaved = true;
            SetStatus("File Saved", StatusMessageState.Information);
        }

        /// <summary>
        /// Save the current document as a new file.
        /// </summary>
        /// <returns>The path to the saved file.</returns>
        public string SaveDocumentAs()
        {
            SetStatus("Saving file...", StatusMessageState.Loading);
            System.Windows.Forms.SaveFileDialog saveDialog = new System.Windows.Forms.SaveFileDialog();
            saveDialog.AddExtension = true;
            saveDialog.CheckPathExists = true;
            saveDialog.SupportMultiDottedExtensions = true;
            saveDialog.Filter = "JQL File|*.jql";
            saveDialog.DefaultExt = ".jql";
            saveDialog.OverwritePrompt = true;
            saveDialog.Title = "Save As...";
            saveDialog.FileOk += (fs, fe) =>
            {
                SaveDocument(saveDialog.FileName);
            };
            saveDialog.ShowDialog();
            return saveDialog.FileName;
        }

        /// <summary>
        /// Open a file choosen by the user.
        /// </summary>
        /// <returns>The path to the file.</returns>
        public string OpenDocument()
        {
            SetStatus("Opening file...", StatusMessageState.Loading);
            System.Windows.Forms.OpenFileDialog openDialog = new System.Windows.Forms.OpenFileDialog();
            openDialog.AddExtension = true;
            openDialog.CheckFileExists = true;
            openDialog.Filter = "JQL File|*.jql";
            openDialog.CheckPathExists = true;
            openDialog.SupportMultiDottedExtensions = true;
            openDialog.DefaultExt = ".jql";
            openDialog.Title = "Open File";
            openDialog.FileOk += (fs, fe) =>
            {
                OpenDocumentAt(openDialog.FileName);
            };
            openDialog.ShowDialog();
            return openDialog.FileName;
        }

        /// <summary>
        /// Open the file at the given path.
        /// </summary>
        /// <param name="path">The path of the file</param>
        public void OpenDocumentAt(string path)
        {
            if (!path.EndsWith(".jql"))
            {
                path = path + ".jql";
            }
            if (Util.Exists(path))
            {
                TextEditor.ResetUndoRedoStack();
                TextEditor.SetDocumentContents(Util.ReadTextFile(path));
                changesSaved = true;
                SetStatus("File Opened", StatusMessageState.Information);
                Title = path + " - JQL Editor";
            }
            else
            {
                new MessageWindow(
                    this,
                    "The file \"" + path + "\" doesn't exists.",
                    "Cannot open the file",
                    MessageWindowButton.OK,
                    MessageWindowImage.Error).Open();
            }
        }

        private void QueryValidator(bool autoSelect = false)
        {
            if (TextEditor.Text.Length > 0)
            {
                SetStatus("Validating query...", StatusMessageState.Loading);
                try
                {
                    QueryParser.MultilineParse(TextEditor.Text);
                    SetStatus("No Errors", StatusMessageState.Information);
                }
                catch (MultilineQueryParseException err)
                {
                    if (autoSelect)
                    {
                        TextEditor.SelectQueryBlock(err.Line - 1);
                    }
                    SetStatus(err.ToString(), StatusMessageState.Error);
                }
                catch (Exception err)
                {
                    SetStatus(err.Message, StatusMessageState.Error);
                }
            }
            else
            {
                SetStatus("No Query.", StatusMessageState.Information);
            }
        }

        private void ValidateQueries(object sender, RoutedEventArgs e)
        {
            QueryValidator(true);
        }

        private void SetStatus(string message, StatusMessageState state)
        {
            switch (state)
            {
                case StatusMessageState.Information:
                    StatusBar.Background = (Brush)(new BrushConverter().ConvertFrom("#0066CC"));
                    StatusBar.Foreground = Brushes.White;
                    break;
                case StatusMessageState.Error:
                    StatusBar.Background = (Brush)(new BrushConverter().ConvertFrom("#CC0000"));
                    StatusBar.Foreground = Brushes.White;
                    break;
                case StatusMessageState.Loading:
                    StatusBar.Background = (Brush)(new BrushConverter().ConvertFrom("#FF9900"));
                    StatusBar.Foreground = Brushes.White;
                    break;
                default:
                    break;
            }

            StatusMessage.Content = message;
        }

        private void Undo(object sender, RoutedEventArgs e)
        {
            TextEditor.Undo();
        }

        private void Redo(object sender, RoutedEventArgs e)
        {
            TextEditor.Redo();
        }

        private void SelectAll(object sender, RoutedEventArgs e)
        {
            TextEditor.SelectAll();
        }

        private void SelectNone(object sender, RoutedEventArgs e)
        {
            TextEditor.Select(0, 0);
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!changesSaved)
            {
                MessageWindowResult choice = new MessageWindow(
                    this,
                    "The current document is not saved, do you want to save it before close?",
                    "Save File",
                    MessageWindowButton.YesNoCancel,
                    MessageWindowImage.Warning).Open();

                switch (choice)
                {
                    case MessageWindowResult.Cancel:
                        e.Cancel = true;
                        break;
                    case MessageWindowResult.Yes:
                        if (App.CurrentWorkingFile == String.Empty)
                        {
                            App.CurrentWorkingFile = SaveDocumentAs();
                        }
                        else
                        {
                            SaveDocument(App.CurrentWorkingFile);
                        }
                        break;
                }
            }
            base.OnClosing(e);
        }

        private void NewFile(object sender, RoutedEventArgs e)
        {
            if (!changesSaved)
            {
                MessageWindowResult choice = new MessageWindow(
                    this,
                    "The current document is not saved, do you want to save it?",
                    "Save File",
                    MessageWindowButton.YesNoCancel,
                    MessageWindowImage.Warning).Open();

                switch (choice)
                {
                    case MessageWindowResult.Cancel:
                        e.Handled = true;
                        break;
                    case MessageWindowResult.Yes:
                        if (App.CurrentWorkingFile == String.Empty)
                        {
                            App.CurrentWorkingFile = SaveDocumentAs();
                        }
                        else
                        {
                            SaveDocument(App.CurrentWorkingFile);
                        }
                        break;
                }
            }
            if (!e.Handled)
            {
                TextEditor.CleanDocument();
                TextEditor.ResetUndoRedoStack();
                App.CurrentWorkingFile = String.Empty;
            }
        }

        private void OpenFile(object sender, RoutedEventArgs e)
        {
            App.CurrentWorkingFile = OpenDocument();
        }

        private void SaveFile(object sender, RoutedEventArgs e)
        {
            if (App.CurrentWorkingFile == String.Empty)
            {
                SaveDocumentAs();
            }
            else
            {
                SaveDocument(App.CurrentWorkingFile);
            }
        }

        private void SaveFileAs(object sender, RoutedEventArgs e)
        {
            SaveDocumentAs();
        }

        private void CleanDocument(object sender, RoutedEventArgs e)
        {
            TextEditor.CleanDocument();
        }

        private void Copy(object sender, RoutedEventArgs e)
        {
            TextEditor.Copy();
        }

        private void Cut(object sender, RoutedEventArgs e)
        {
            TextEditor.Cut();
        }

        private void Paste(object sender, RoutedEventArgs e)
        {
            TextEditor.Paste();
        }

        private void Delete(object sender, RoutedEventArgs e)
        {
            string currentText = TextEditor.GetDocumentContents();
            if (TextEditor.SelectedText != String.Empty)
            {
                currentText = currentText.Remove(TextEditor.SelectionStart, TextEditor.SelectionLength);
            }
            else
            {
                currentText = currentText.Remove(TextEditor.CaretIndex, 1);
            }
            TextEditor.SetDocumentContents(currentText, true);
        }

        private void CanExecuteAlwaysTrue(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void Exit_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void New_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            NewFile(sender, e);
        }

        private void Open_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFile(sender, e);
        }

        private void Save_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFile(sender, e);
        }

        private void SaveAs_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileAs(sender, e);
        }

        private void Undo_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            Undo(sender, e);
        }

        private void Redo_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            Redo(sender, e);
        }

        private void SetBlackTheme(object sender, RoutedEventArgs e)
        {
            TextEditor.Foreground = (Brush)(new BrushConverter().ConvertFrom("#333333"));
            TextEditor.Background = (Brush)(new BrushConverter().ConvertFrom("#252121"));
            TextEditor.TextColor = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));
        }

        private void SetWhiteTheme(object sender, RoutedEventArgs e)
        {
            TextEditor.Foreground = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));
            TextEditor.Background = (Brush)(new BrushConverter().ConvertFrom("#e5e5e5"));
            TextEditor.TextColor = (Brush)(new BrushConverter().ConvertFrom("#000000"));
        }

        private void ShowLineNumbers(object sender, RoutedEventArgs e)
        {
            TextEditor.IsLineNumbersMarginVisible = MenuViewShowLineNumbers.IsChecked;
        }
    }

    public static class EditorCommands
    {
        public static readonly RoutedUICommand Exit = new RoutedUICommand
        (
            "Exit JQL Editor",
            "Exit",
            typeof(EditorCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.F4, ModifierKeys.Alt),
                new KeyGesture(Key.Q, ModifierKeys.Control)
            }
        );

        public static readonly RoutedUICommand New = new RoutedUICommand
        (
            "New JQL File",
            "New",
            typeof(EditorCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.N, ModifierKeys.Control)
            }
        );

        public static readonly RoutedUICommand Open = new RoutedUICommand
        (
            "Open JQL File",
            "Open",
            typeof(EditorCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.O, ModifierKeys.Control)
            }
        );

        public static readonly RoutedUICommand Save = new RoutedUICommand
        (
            "Save JQL File",
            "Save",
            typeof(EditorCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.S, ModifierKeys.Control)
            }
        );

        public static readonly RoutedUICommand SaveAs = new RoutedUICommand
        (
            "Save JQL File As...",
            "Save As...",
            typeof(EditorCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)
            }
        );

        public static readonly RoutedUICommand Undo = new RoutedUICommand
        (
            "Undo",
            "Undo",
            typeof(EditorCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.Z, ModifierKeys.Control)
            }
        );

        public static readonly RoutedUICommand Redo = new RoutedUICommand
        (
            "Redo",
            "Redo",
            typeof(EditorCommands),
            new InputGestureCollection()
            {
                new KeyGesture(Key.Y, ModifierKeys.Control)
            }
        );
    }

}
