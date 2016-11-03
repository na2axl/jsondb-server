using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace JSONDB.JQLEditor.TextEditor
{
    public partial class SyntaxHighlightBox : TextBox
    {

        // ----------------------------------------------------------
        // Attributes
        // ----------------------------------------------------------

        /// <summary>
        /// Get or set the value of the line height.
        /// </summary>
        public double LineHeight
        {
            get { return lineHeight; }
            set
            {
                if (value != lineHeight)
                {
                    lineHeight = value;
                    blockHeight = MaxLineCountInBlock * value;
                    TextBlock.SetLineStackingStrategy(this, LineStackingStrategy.BlockLineHeight);
                    TextBlock.SetLineHeight(this, lineHeight);
                }
            }
        }

        /// <summary>
        /// Get or set the maximum number of lines in a block.
        /// </summary>
        public int MaxLineCountInBlock
        {
            get { return maxLineCountInBlock; }
            set
            {
                maxLineCountInBlock = value > 0 ? value : 0;
                blockHeight = value * LineHeight;
            }
        }

        // ----------------------------------------------------------
        // Fields
        // ----------------------------------------------------------

        private Canvas suggestionCanvas;
        private ListBox suggestionList;
        private DrawingControl renderCanvas;
        private DrawingControl lineNumbersCanvas;
        private Line lineNumbersSeparator;
        private ScrollViewer scrollViewer;
        private double lineHeight;
        private int totalLineCount;
        private List<InnerTextBlock> blocks;
        private double blockHeight;
        private int maxLineCountInBlock;
        private UndoRedoStack<TextState> stack = new UndoRedoStack<TextState>();
        private bool cancelNextStack = false;

        // ----------------------------------------------------------
        // Ctor and event handlers
        // ----------------------------------------------------------

        /// <summary>
        /// Create a new SyntaxHighlightBox instance.
        /// </summary>
        public SyntaxHighlightBox()
        {
            InitializeComponent();

            MaxLineCountInBlock = 100;
            LineHeight = FontSize * 1.3;
            totalLineCount = 1;
            blocks = new List<InnerTextBlock>();
            IsUndoEnabled = false;

            stack.Push(new TextStack());

            Loaded += (s, e) =>
            {
                renderCanvas = (DrawingControl)Template.FindName("PART_RenderCanvas", this);
                lineNumbersCanvas = (DrawingControl)Template.FindName("PART_LineNumbersCanvas", this);
                scrollViewer = (ScrollViewer)Template.FindName("PART_ContentHost", this);
                lineNumbersSeparator = (Line)Template.FindName("lineNumbersSeparator", this);

                lineNumbersCanvas.Width = GetFormattedTextWidth(string.Format("{0:0000}", totalLineCount)) + 5;

                suggestionCanvas = (Canvas)Template.FindName("PART_SuggestionCanvas", this);
                suggestionList = (ListBox)Template.FindName("PART_SuggestionList", this);

                scrollViewer.ScrollChanged += OnScrollChanged;

                suggestionList.PreviewKeyDown += (kds, kde) =>
                {
                    // Hide Intellisense List
                    if (kde.Key == Key.Escape)
                    {
                        HideSuggestionList();
                        kde.Handled = true;
                    }

                    // Navigate through the Intellisense list
                    else if (kde.Key == Key.Up)
                    {
                        suggestionList.Items.MoveCurrentToPrevious();
                        if (suggestionList.Items.IsCurrentBeforeFirst)
                        {
                            suggestionList.Items.MoveCurrentToFirst();
                        }
                        ((IntellisenseListItem)suggestionList.Items.CurrentItem).Focus();
                        kde.Handled = true;
                    }
                    else if (kde.Key == Key.Down)
                    {
                        suggestionList.Items.MoveCurrentToNext();
                        if (suggestionList.Items.IsCurrentAfterLast)
                        {
                            suggestionList.Items.MoveCurrentToLast();
                        }
                        ((IntellisenseListItem)suggestionList.Items.CurrentItem).Focus();
                        kde.Handled = true;
                    }

                    // Execute the Intellisense item action
                    else if (kde.Key == Key.Enter || kde.Key == Key.Tab)
                    {
                        if (suggestionList.Items.CurrentItem != null)
                        {
                            ((IntellisenseListItem)suggestionList.Items.CurrentItem).Action();
                            kde.Handled = true;
                        }
                    }

                    // Go back to the editor
                    else
                    {
                        Focus();
                    }
                };

                HideSuggestionList();

                InvalidateBlocks(0);
                InvalidateVisual();
            };

            SizeChanged += (s, e) =>
            {
                if (e.HeightChanged == false)
                {
                    return;
                }
                UpdateBlocks();
                InvalidateVisual();
            };

            PreviewTextInput += (s, e) =>
            {
                // Auto open the Intellisense list...
                if (e.Text == "." && !SuggestionListIsVisible())
                {
                    if (SelectedText == String.Empty)
                    {
                        int LastCaretPos = CaretIndex;
                        Text = Text.Insert(LastCaretPos, ".");
                        CaretIndex = LastCaretPos + 1;
                        ShowSuggestionList();
                        e.Handled = true;
                    }
                }

                // Auto closing...
                else if (e.Text == "(")
                {
                    // ...the list
                    HideSuggestionList();
                    // ...the parenthesis if not escaped
                    if (!Text.EndsWith("\\"))
                    {
                        int LastCaretPos = CaretIndex;
                        Text = Text.Insert(LastCaretPos, ")");
                        CaretIndex = LastCaretPos;
                    }
                }
            };

            TextChanged += (s, e) =>
            {
                // Manually manage the Undo/Redo stack
                if (!cancelNextStack)
                {
                    stack.Push(new TextStack(Text, CaretIndex));
                }
                else
                {
                    cancelNextStack = false;
                }

                // Filter the list
                FilterSuggestionList();

                UpdateTotalLineCount();
                InvalidateBlocks(e.Changes.First().Offset);
                InvalidateVisual();
            };

            PreviewKeyDown += (s, e) =>
            {
                int LastCaretPos = CaretIndex;

                // Update Intellisense list position
                UpdateSuggestionListPosition();

                // Show Intellisense list
                if (e.Key == Key.Space && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                {
                    ShowSuggestionList();
                    e.Handled = true;
                }

                // Hide Intellisense List...
                if (e.Key == Key.Escape)
                {
                    HideSuggestionList();
                    e.Handled = true;
                }

                // If pressing Up or Down when Intellisense is visible and not focused
                if (SuggestionListIsVisible() && IsFocused)
                {
                    if (e.Key == Key.Up || e.Key == Key.Down)
                    {
                        if (suggestionList.Items.CurrentItem != null)
                        {
                            ((IntellisenseListItem)suggestionList.Items.CurrentItem).Focus();
                        }
                        else
                        {
                            suggestionList.Focus();
                        }
                        e.Handled = true;
                    }
                }

                // Shift Key + Tab (BackTab) 
                if (e.Key == Key.Tab && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
                {
                    if (SelectedText != String.Empty)
                    {
                        string[] lines = SelectedText.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].StartsWith(Tab))
                            {
                                lines[i] = lines[i].Substring(Tab.Length);
                            }
                            else
                            {
                                lines[i] = lines[i].TrimStart(' ');
                            }
                        }
                        SelectedText = String.Join(Environment.NewLine, lines);
                    }
                    else
                    {
                        int last_line = Text.LastIndexOf(Environment.NewLine, LastCaretPos);

                        if (last_line == -1)
                        {
                            last_line = Text.Length - 1;
                        }

                        int start_line = Text.IndexOf(Environment.NewLine, last_line);

                        if (start_line != -1)
                        {
                            start_line += Environment.NewLine.Length;
                        }
                        else
                        {
                            start_line = 0;
                        }

                        int spaces = 0;
                        for (int i = start_line; i < Text.Length - 1; i++)
                        {
                            if (Text[i] == ' ')
                            {
                                spaces++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (spaces > TabSize)
                        {
                            spaces = TabSize;
                        }

                        Text = Text.Remove(start_line, spaces);

                        if (LastCaretPos >= start_line + spaces)
                        {
                            CaretIndex = LastCaretPos - spaces;
                        }
                        else if (LastCaretPos >= start_line && LastCaretPos < start_line + spaces)
                        {
                            CaretIndex = start_line;
                        }
                        else
                        {
                            CaretIndex = LastCaretPos;
                        }
                    }

                    e.Handled = true;
                }

                // Only if the Intellisense list is hidden or we have focus
                if (!SuggestionListIsVisible() || IsFocused)
                {
                    // Tab Key
                    if (e.Key == Key.Tab && e.KeyboardDevice.Modifiers == ModifierKeys.None)
                    {
                        // If we are here and the suggestion list is visible, don't insert tab
                        if (SuggestionListIsVisible())
                        {
                            if (SuggestionListHasItems())
                            {
                                if (suggestionList.Items.CurrentItem != null)
                                {
                                    ((IntellisenseListItem)suggestionList.Items.CurrentItem).Action();
                                }
                                else
                                {
                                    ((IntellisenseListItem)suggestionList.Items[0]).Action();
                                }
                            }
                            else
                            {
                                HideSuggestionList();
                                Focus();
                            }
                        }
                        // Otherwise...
                        else if (SelectedText == String.Empty)
                        {
                            Text = Text.Insert(LastCaretPos, Tab);
                            CaretIndex = LastCaretPos + TabSize;
                        }
                        else
                        {
                            if (!SelectedText.Contains(Environment.NewLine))
                            {
                                SelectedText = Tab;
                            }
                            else
                            {
                                SelectedText = Tab + SelectedText.Replace(Environment.NewLine, Environment.NewLine + Tab);
                            }
                        }

                        e.Handled = true;
                    }

                    // Enter Key
                    if (e.Key == Key.Return)
                    {
                        // If we are here and the suggestion list is visible, then we have focus... Hide the list
                        HideSuggestionList();

                        int last_line = Text.LastIndexOf(Environment.NewLine, LastCaretPos);
                        int spaces = 0;

                        if (last_line != -1)
                        {
                            string line = Text.Substring(last_line, Text.Length - last_line);

                            int start_line = line.IndexOf(Environment.NewLine);

                            if (start_line != -1)
                            {
                                line = line.Substring(start_line).TrimStart(Environment.NewLine.ToCharArray());
                            }

                            foreach (char c in line)
                            {
                                if (c == ' ')
                                {
                                    spaces++;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        Text = Text.Insert(LastCaretPos, Environment.NewLine + new String(' ', spaces));
                        CaretIndex = LastCaretPos + Environment.NewLine.Length + spaces;

                        e.Handled = true;
                    }
                }

                // Undo/Redo
                if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.Z)
                    {
                        Undo();
                    }
                    else if (e.Key == Key.Y)
                    {
                        Redo();
                    }
                }
            };
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            DrawBlocks();
            base.OnRender(drawingContext);
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                UpdateBlocks();
            }
            InvalidateVisual();
        }

        // ----------------------------------------------------------
        // Updating & Block managing
        // ----------------------------------------------------------

        private void UpdateTotalLineCount()
        {
            totalLineCount = TextUtilities.GetLineCount(Text);
        }

        private void UpdateBlocks()
        {
            if (blocks.Count == 0)
            {
                return;
            }

            // While something is visible after last block...
            while (!blocks.Last().IsLast && blocks.Last().Position.Y + blockHeight - VerticalOffset < ActualHeight)
            {
                int firstLineIndex = blocks.Last().LineEndIndex + 1;
                int lastLineIndex = firstLineIndex + maxLineCountInBlock - 1;
                lastLineIndex = lastLineIndex <= totalLineCount - 1 ? lastLineIndex : totalLineCount - 1;

                int firstCharIndex = blocks.Last().CharEndIndex + 1;
                int lastCharIndex = TextUtilities.GetLastCharIndexFromLineIndex(Text, lastLineIndex);

                if (lastCharIndex <= firstCharIndex)
                {
                    blocks.Last().IsLast = true;
                    return;
                }

                InnerTextBlock block = new InnerTextBlock(
                    firstCharIndex,
                    lastCharIndex,
                    blocks.Last().LineEndIndex + 1,
                    lastLineIndex,
                    LineHeight);
                block.RawText = block.GetSubString(Text);
                block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
                blocks.Add(block);
                FormatBlock(block, blocks.Count > 1 ? blocks[blocks.Count - 2] : null);
            }
        }

        private void InvalidateBlocks(int changeOffset)
        {
            InnerTextBlock blockChanged = null;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].CharStartIndex <= changeOffset && changeOffset <= blocks[i].CharEndIndex + 1)
                {
                    blockChanged = blocks[i];
                    break;
                }
            }

            if (blockChanged == null && changeOffset > 0)
            {
                blockChanged = blocks.Last();
            }

            int fvline = blockChanged != null ? blockChanged.LineStartIndex : 0;
            int lvline = GetIndexOfLastVisibleLine();
            int fvchar = blockChanged != null ? blockChanged.CharStartIndex : 0;
            int lvchar = TextUtilities.GetLastCharIndexFromLineIndex(Text, lvline);

            if (blockChanged != null)
            {
                blocks.RemoveRange(blocks.IndexOf(blockChanged), blocks.Count - blocks.IndexOf(blockChanged));
            }

            int localLineCount = 1;
            int charStart = fvchar;
            int lineStart = fvline;
            for (int i = fvchar; i < Text.Length; i++)
            {
                if (Text[i] == '\n')
                {
                    localLineCount += 1;
                }
                if (i == Text.Length - 1)
                {
                    string blockText = Text.Substring(charStart);
                    InnerTextBlock block = new InnerTextBlock(
                        charStart,
                        i, lineStart,
                        lineStart + TextUtilities.GetLineCount(blockText) - 1,
                        LineHeight);
                    block.RawText = block.GetSubString(Text);
                    block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
                    block.IsLast = true;

                    foreach (InnerTextBlock b in blocks)
                    {
                        if (b.LineStartIndex == block.LineStartIndex)
                        {
                            throw new Exception("Internal error occured.");
                        }
                    }

                    blocks.Add(block);
                    FormatBlock(block, blocks.Count > 1 ? blocks[blocks.Count - 2] : null);
                    break;
                }
                if (localLineCount > maxLineCountInBlock)
                {
                    InnerTextBlock block = new InnerTextBlock(
                        charStart,
                        i,
                        lineStart,
                        lineStart + maxLineCountInBlock - 1,
                        LineHeight);
                    block.RawText = block.GetSubString(Text);
                    block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);

                    foreach (InnerTextBlock b in blocks)
                    {
                        if (b.LineStartIndex == block.LineStartIndex)
                        {
                            throw new Exception("Internal error occured.");
                        }
                    }

                    blocks.Add(block);
                    FormatBlock(block, blocks.Count > 1 ? blocks[blocks.Count - 2] : null);

                    charStart = i + 1;
                    lineStart += maxLineCountInBlock;
                    localLineCount = 1;

                    if (i > lvchar)
                    {
                        break;
                    }
                }
            }
        }

        // ----------------------------------------------------------
        // Rendering
        // ----------------------------------------------------------

        private void DrawBlocks()
        {
            if (!IsLoaded || renderCanvas == null || lineNumbersCanvas == null)
            {
                return;
            }

            var dc = renderCanvas.GetContext();
            var dc2 = lineNumbersCanvas.GetContext();
            for (int i = 0; i < blocks.Count; i++)
            {
                InnerTextBlock block = blocks[i];
                Point blockPos = block.Position;
                double top = blockPos.Y - VerticalOffset;
                double bottom = top + blockHeight;
                if (top < ActualHeight && bottom > 0)
                {
                    try
                    {
                        dc.DrawText(block.FormattedText, new Point(2 - HorizontalOffset, block.Position.Y - VerticalOffset));
                        if (IsLineNumbersMarginVisible)
                        {
                            lineNumbersCanvas.Width = GetFormattedTextWidth(string.Format("{0:0000}", totalLineCount)) + 5;
                            dc2.DrawText(block.LineNumbers, new Point(lineNumbersCanvas.ActualWidth, 1 + block.Position.Y - VerticalOffset));
                        }
                    }
                    catch
                    {
                        // An error occur when pasting huge text with ctrl + v holded...
                    }
                }
            }
            dc.Close();
            dc2.Close();
        }

        /// <summary>
        /// Show suggestion list
        /// </summary>
        private void ShowSuggestionList(string text = null)
        {
            // Hide the suggestion List
            HideSuggestionList();

            // Generate Intellisense List
            PopulateSuggestionList(text, null);

            // Update position
            UpdateSuggestionListPosition();

            // Show the List if there is something to show
            if (suggestionList.Items.Count > 0)
            {
                suggestionCanvas.IsHitTestVisible = true;
                suggestionList.Visibility = Visibility.Visible;
                suggestionList.Focus();
            }
        }

        /// <summary>
        /// Add items to the Intellisense list.
        /// </summary>
        /// <param name="text">The text used to determine which item will be added.</param>
        private void PopulateSuggestionList(string text, int? pos)
        {
            suggestionList.Items.Clear();
            string LeftCaretText = text ?? Text.Substring(0, CaretIndex);
            if (Regex.IsMatch(LeftCaretText, "\\w+[^)\\\\]?[\\r\\n\\t ]*\\.$"))
            {
                int LastCaretPos = pos ?? CaretIndex;
                suggestionList.Items.Add(new IntellisenseListItem("count", "count()", "Count the number of rows in a table.", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "count()");
                    CaretIndex = LastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("delete", "delete()", "Remove the value of rows and columns in a table.", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "delete()");
                    CaretIndex = LastCaretPos + 7;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("insert", "insert()", "Add a new row in a table.", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "insert()");
                    CaretIndex = LastCaretPos + 7;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("update", "update()", "Update rows and columns with a new value.", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "update()");
                    CaretIndex = LastCaretPos + 7;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("select", "select()", "Retrieve data from a table.", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "select()");
                    CaretIndex = LastCaretPos + 7;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("truncate", "truncate()", "Delete all data and reset the table.", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "truncate()");
                    CaretIndex = LastCaretPos + 9;
                    Focus();
                    HideSuggestionList();
                }));
            }
            else if (Regex.IsMatch(LeftCaretText, "\\)[\\r\\n\\t ]*\\.$"))
            {
                int LastCaretPos = pos ?? CaretIndex;
                suggestionList.Items.Add(new IntellisenseListItem("as", "as()", "Create an alias for a query parameter. Use it with select() and count().", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "as()");
                    CaretIndex = LastCaretPos + 3;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("group", "group()", "Group the number of value's occurences. Use it with count().", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "group()");
                    CaretIndex = LastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("in", "in()", "Define in which columns we have to insert data. Use it with insert().", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "in()");
                    CaretIndex = LastCaretPos + 3;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("limit", "limit()", "Limit the number of retrieved results. Use it with select().", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "limit()");
                    CaretIndex = LastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("on", "on()", "Define which action to execute on a column. Use it with select().", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "on()");
                    CaretIndex = LastCaretPos + 3;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("order", "order()", "Reorder retrieved results. Use it with select().", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "order()");
                    CaretIndex = LastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("where", "where()", "Apply conditions to the query.", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "where()");
                    CaretIndex = LastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                suggestionList.Items.Add(new IntellisenseListItem("with", "with()", "Define which values to use. Use it with update().", () =>
                {
                    if (CaretIndex - LastCaretPos > 0)
                    {
                        Text = Text.Remove(LastCaretPos, CaretIndex - LastCaretPos);
                    }
                    Text = Text.Insert(LastCaretPos, "with()");
                    CaretIndex = LastCaretPos + 5;
                    Focus();
                    HideSuggestionList();
                }));
            }
        }

        /// <summary>
        /// Update the Intellisense list.
        /// </summary>
        private void UpdateSuggestionListPosition()
        {
            Point position = GetRectFromCharacterIndex(CaretIndex).BottomRight;

            double left = position.X - lineNumbersCanvas.ActualWidth - lineNumbersCanvas.Margin.Left - lineNumbersCanvas.Margin.Right - lineNumbersSeparator.Margin.Left - lineNumbersSeparator.Margin.Right - Padding.Left - Margin.Left - Padding.Right - Margin.Right;
            double top = position.Y - Padding.Top;

            if (left + suggestionList.ActualWidth > suggestionCanvas.ActualWidth)
            {
                left = suggestionCanvas.ActualWidth - suggestionList.ActualWidth - Padding.Right - Margin.Right;
            }

            if (top + suggestionList.ActualHeight > suggestionCanvas.ActualHeight)
            {
                top = suggestionCanvas.ActualHeight - suggestionList.ActualHeight - Padding.Bottom - Margin.Bottom;
            }

            Canvas.SetLeft(suggestionList, left);
            Canvas.SetTop(suggestionList, top);
        }

        /// <summary>
        /// Hide suggestion list
        /// </summary>
        private void HideSuggestionList()
        {
            suggestionCanvas.IsHitTestVisible = false;
            suggestionList.Visibility = Visibility.Collapsed;
        }

        // ----------------------------------------------------------
        // Utilities
        // ----------------------------------------------------------

        /// <summary>
        /// Get the content of the document.
        /// </summary>
        /// <returns>The current document's content</returns>
        public string GetDocumentContents()
        {
            return Text;
        }

        /// <summary>
        /// Set the content of the document.
        /// </summary>
        /// <param name="text">The new document's content</param>
        /// <param name="saveCaretPosition">The caret have the same position after insertion</param>
        public void SetDocumentContents(string text, bool saveCaretPosition = false)
        {
            int LastCaretPos = 0;
            if (saveCaretPosition)
            {
                LastCaretPos = CaretIndex;
            }
            Text = text;
            CaretIndex = LastCaretPos;
        }

        /// <summary>
        /// Clean the current document.
        /// </summary>
        public void CleanDocument()
        {
            Text = String.Empty;
        }

        /// <summary>
        /// Check if the user can do an undo operation.
        /// </summary>
        /// <returns>true if it's possible, and false otherwise.</returns>
        public new bool CanUndo()
        {
            return stack.UndoCount > 0;
        }

        /// <summary>
        /// Check if the user can do a redo operation.
        /// </summary>
        /// <returns>true if it's possible, and false otherwise.</returns>
        public new bool CanRedo()
        {
            return stack.RedoCount > 0;
        }

        /// <summary>
        /// Undo an operation in the stack.
        /// </summary>
        public new void Undo()
        {
            HideSuggestionList();
            var thisStack = stack.UnPush(new TextStack(Text, CaretIndex));
            if (thisStack != null)
            {
                cancelNextStack = true;
                TextState State = thisStack.Do(((TextStack)thisStack).State);
                Text = State.Text;
                CaretIndex = State.CaretIndex;
            }
        }

        /// <summary>
        /// Redo an operation in the stack.
        /// </summary>
        public new void Redo()
        {
            HideSuggestionList();
            var thisStack = stack.RePush(new TextStack(Text, CaretIndex));
            if (thisStack != null)
            {
                cancelNextStack = true;
                TextState State = thisStack.Do(((TextStack)thisStack).State);
                Text = State.Text;
                CaretIndex = State.CaretIndex;
            }
        }

        /// <summary>
        /// Reset the undo/redo stack.
        /// </summary>
        public void ResetUndoRedoStack()
        {
            stack.Reset();
        }

        /// <summary>
        /// Filter the Intellisense list to display only items who matches the text entered.
        /// </summary>
        public void FilterSuggestionList()
        {
            if (SuggestionListIsVisible())
            {
                int LastCaretPosititon = CaretIndex;

                int ActiveLineIndex = GetIndexOfActiveLine() - 1;
                int FirstCharIndex = TextUtilities.GetFirstCharIndexFromLineIndex(Text, ActiveLineIndex);
                int LastCharIndex = TextUtilities.GetLastCharIndexFromLineIndex(Text, ActiveLineIndex);

                string LineString = Text.Substring(FirstCharIndex, LastCharIndex - FirstCharIndex);
                int LastDotPosition = LineString.LastIndexOf('.');

                string SuggestionLabelPart = LineString.Substring(LastDotPosition + 1);

                // Refresh the list first
                for (int i = 0, l = suggestionList.Items.Count; i < l; i++)
                {
                    IntellisenseListItem item = (IntellisenseListItem)suggestionList.Items[i];
                    item.Visibility = Visibility.Visible;
                }

                int h = 0;
                for (int i = 0, l = suggestionList.Items.Count; i < l; i++)
                {
                    IntellisenseListItem item = (IntellisenseListItem)suggestionList.Items[i];
                    if (!Regex.IsMatch(item.DisplayText, "^" + Regex.Escape(SuggestionLabelPart)))
                    {
                        item.Visibility = Visibility.Collapsed;
                        h++;
                    }
                    else
                    {
                        item.Focus();
                    }
                }

                // If all items are hidden
                if (h == suggestionList.Items.Count)
                {
                    HideSuggestionList();
                }
            }
        }

        /// <summary>
        /// Check if the suggestion list is visible.
        /// </summary>
        /// <returns>true if visible, false otherwise</returns>
        public bool SuggestionListIsVisible()
        {
            return suggestionList.Visibility == Visibility.Visible;
        }

        /// <summary>
        /// Check if the suggestion list have at least one item.
        /// </summary>
        /// <returns></returns>
        public bool SuggestionListHasItems()
        {
            return suggestionList.Items.Count > 0;
        }

        /// <summary>
        /// Select a query block.
        /// </summary>
        /// <param name="blockNumber">The query's block ID to select.</param>
        public void SelectQueryBlock(int blockNumber)
        {
            string[] blocks = Regex.Split(Text, "(?:[^\\\\]);");

            Match currentBlock = Regex.Match(Text, Regex.Escape(blocks[blockNumber].Trim(Environment.NewLine.ToCharArray())));

            Select(currentBlock.Index, currentBlock.Length + 2);
        }

        /// <summary>
        /// Get the index of the active line.
        /// </summary>
        /// <returns>The non zero based index of the active line</returns>
        public int GetIndexOfActiveLine()
        {
            return Regex.Split(Text.Substring(0, CaretIndex + 1 < Text.Length ? CaretIndex + 1 : CaretIndex), Environment.NewLine).Length;
        }

        /// <summary>
        /// Get the text of the active line.
        /// </summary>
        /// <returns>The text of the active line</returns>
        public string GetTextOfActiveLine()
        {
            return Regex.Split(Text, Environment.NewLine)[GetIndexOfActiveLine()];
        }

        /// <summary>
        /// Get the text at the given line.
        /// </summary>
        /// <param name="line">The non zero based line number</param>
        /// <returns>The text at the given line</returns>
        public string GetTextAtLine(int line)
        {
            return Regex.Split(Text, Environment.NewLine)[line - 1];
        }

        /// <summary>
        /// Returns the index of the first visible text line.
        /// </summary>
        public int GetIndexOfFirstVisibleLine()
        {
            int guessedLine = (int)(VerticalOffset / lineHeight);
            return guessedLine > totalLineCount ? totalLineCount : guessedLine;
        }

        /// <summary>
        /// Returns the index of the last visible text line.
        /// </summary>
        public int GetIndexOfLastVisibleLine()
        {
            double height = VerticalOffset + ViewportHeight;
            int guessedLine = (int)(height / lineHeight);
            return guessedLine > totalLineCount - 1 ? totalLineCount - 1 : guessedLine;
        }

        /// <summary>
        /// Formats and Highlights the text of a block.
        /// </summary>
        private void FormatBlock(InnerTextBlock currentBlock, InnerTextBlock previousBlock)
        {
            currentBlock.FormattedText = GetFormattedText(currentBlock.RawText);
            if (CurrentHighlighter != null)
            {
                int previousCode = previousBlock != null ? previousBlock.Code : -1;
                currentBlock.Code = CurrentHighlighter.HighlightBlock(currentBlock.FormattedText, previousCode);
            }
        }

        /// <summary>
        /// Returns a formatted text object from the given string
        /// </summary>
        private FormattedText GetFormattedText(string text)
        {
            FormattedText ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize,
                TextColor);

            ft.Trimming = TextTrimming.None;
            ft.LineHeight = lineHeight;

            return ft;
        }

        /// <summary>
        /// Returns a string containing a list of numbers separated with newlines.
        /// </summary>
        private FormattedText GetFormattedLineNumbers(int firstIndex, int lastIndex)
        {
            string text = "";
            for (int i = firstIndex + 1; i <= lastIndex + 1; i++)
            {
                text += i.ToString() + Environment.NewLine;
            }
            text = text.Trim();

            FormattedText ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize,
                new SolidColorBrush(Color.FromRgb(0x21, 0xA1, 0xD8)));

            ft.Trimming = TextTrimming.None;
            ft.LineHeight = lineHeight;
            ft.TextAlignment = TextAlignment.Right;

            return ft;
        }

        /// <summary>
        /// Returns the width of a text once formatted.
        /// </summary>
        private double GetFormattedTextWidth(string text)
        {
            FormattedText ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize,
                TextColor);

            ft.Trimming = TextTrimming.None;
            ft.LineHeight = lineHeight;

            return ft.Width;
        }

        // ----------------------------------------------------------
        // Others
        // ----------------------------------------------------------

        /// <summary>
        /// String representation of the text box.
        /// </summary>
        /// <returns>Information about the current text state</returns>
        public override string ToString()
        {
            int activeLineIndex = GetIndexOfActiveLine();
            int firstCharIndex = TextUtilities.GetFirstCharIndexFromLineIndex(Text, activeLineIndex - 1);
            int colIndex = Text.Substring(firstCharIndex, CaretIndex - firstCharIndex).Length + 1;
            int charNB = Text.Length;

            string activeLineText = GetTextAtLine(activeLineIndex);

            return String.Format("Ln {0}    Col {1}    Ch {2}/{3}",
                activeLineIndex,
                colIndex,
                CaretIndex,
                charNB);
        }

        // ----------------------------------------------------------
        // Dependency Properties
        // ----------------------------------------------------------

        public static readonly DependencyProperty IsLineNumbersMarginVisibleProperty = DependencyProperty.Register(
            "IsLineNumbersMarginVisible", typeof(bool), typeof(SyntaxHighlightBox), new PropertyMetadata(true));

        public static readonly DependencyProperty TextColorProperty = DependencyProperty.Register(
            "TextColor", typeof(Brush), typeof(SyntaxHighlightBox), new PropertyMetadata(Brushes.Black));

        // ----------------------------------------------------------
        // Properties
        // ----------------------------------------------------------

        public int TabSize
        {
            get { return 4; }
        }

        public IHighlighter CurrentHighlighter { get; set; }

        public bool IsLineNumbersMarginVisible
        {
            get { return (bool)GetValue(IsLineNumbersMarginVisibleProperty); }
            set { SetValue(IsLineNumbersMarginVisibleProperty, value); }
        }

        public Brush TextColor
        {
            get { return (Brush)GetValue(TextColorProperty); }
            set { SetValue(TextColorProperty, value); InvalidateBlocks(0); InvalidateVisual(); }
        }

        private string Tab
        {
            get { return new String(' ', TabSize); }
        }

        // ----------------------------------------------------------
        // Structs
        // ----------------------------------------------------------

        struct TextState
        {
            public string Text { get; set; }
            public int CaretIndex { get; set; }
        }

        // ----------------------------------------------------------
        // Classes
        // ----------------------------------------------------------

        private class InnerTextBlock
        {
            public string RawText { get; set; }
            public FormattedText FormattedText { get; set; }
            public FormattedText LineNumbers { get; set; }
            public int CharStartIndex { get; private set; }
            public int CharEndIndex { get; private set; }
            public int LineStartIndex { get; private set; }
            public int LineEndIndex { get; private set; }
            public Point Position { get { return new Point(0, LineStartIndex * lineHeight); } }
            public bool IsLast { get; set; }
            public int Code { get; set; }

            private double lineHeight;

            public InnerTextBlock(int charStart, int charEnd, int lineStart, int lineEnd, double lineHeight)
            {
                CharStartIndex = charStart;
                CharEndIndex = charEnd;
                LineStartIndex = lineStart;
                LineEndIndex = lineEnd;
                this.lineHeight = lineHeight;
                IsLast = false;

            }

            public string GetSubString(string text)
            {
                int length = CharEndIndex < text.Length ? CharEndIndex - CharStartIndex + 1 : CharEndIndex - CharStartIndex;
                return text.Substring(CharStartIndex, length);
            }

            public override string ToString()
            {
                return String.Format("L:{0}/{1} C:{2}/{3} {4}",
                    LineStartIndex,
                    LineEndIndex,
                    CharStartIndex,
                    CharEndIndex,
                    FormattedText.Text);
            }
        }

        private class TextStack : IStack<TextState>
        {
            private TextState _State;

            public TextState State
            {
                get { return _State; }
                private set { _State = value; }
            }

            public TextStack()
            {
                _State = new TextState();
                _State.Text = String.Empty;
                _State.CaretIndex = 0;
            }

            public TextStack(string text, int caret)
            {
                _State = new TextState();
                _State.CaretIndex = caret;
                _State.Text = text;
            }

            public TextState Do(TextState now)
            {
                return now;
            }

            public TextState Undo(TextState last)
            {
                return _State;
            }
        }
    }
}
