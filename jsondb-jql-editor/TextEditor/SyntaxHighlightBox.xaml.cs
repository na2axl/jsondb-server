using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
            get { return _lineHeight; }
            set
            {
                if (value != _lineHeight)
                {
                    _lineHeight = value;
                    _blockHeight = MaxLineCountInBlock * value;
                    TextBlock.SetLineStackingStrategy(this, LineStackingStrategy.BlockLineHeight);
                    TextBlock.SetLineHeight(this, _lineHeight);
                }
            }
        }

        /// <summary>
        /// Get or set the maximum number of lines in a block.
        /// </summary>
        public int MaxLineCountInBlock
        {
            get { return _maxLineCountInBlock; }
            set
            {
                _maxLineCountInBlock = value > 0 ? value : 0;
                _blockHeight = value * LineHeight;
            }
        }

        // ----------------------------------------------------------
        // Fields
        // ----------------------------------------------------------

        private Canvas _suggestionCanvas;
        private ListBox _suggestionList;
        private DrawingControl _renderCanvas;
        private DrawingControl _lineNumbersCanvas;
        private Line _lineNumbersSeparator;
        private ScrollViewer _scrollViewer;
        private double _lineHeight;
        private int _totalLineCount;
        private List<InnerTextBlock> _blocks;
        private double _blockHeight;
        private int _maxLineCountInBlock;
        private UndoRedoStack<TextState> _stack = new UndoRedoStack<TextState>();
        private bool _cancelNextStack = false;

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
            _totalLineCount = 1;
            _blocks = new List<InnerTextBlock>();
            IsUndoEnabled = false;

            _stack.Push(new TextStack());

            Loaded += (s, e) =>
            {
                _renderCanvas = (DrawingControl)Template.FindName("PART_RenderCanvas", this);
                _lineNumbersCanvas = (DrawingControl)Template.FindName("PART_LineNumbersCanvas", this);
                _scrollViewer = (ScrollViewer)Template.FindName("PART_ContentHost", this);
                _lineNumbersSeparator = (Line)Template.FindName("lineNumbersSeparator", this);

                _lineNumbersCanvas.Width = GetFormattedTextWidth($"{_totalLineCount:0000}") + 5;

                _suggestionCanvas = (Canvas)Template.FindName("PART_SuggestionCanvas", this);
                _suggestionList = (ListBox)Template.FindName("PART_SuggestionList", this);

                _scrollViewer.ScrollChanged += OnScrollChanged;

                _suggestionList.PreviewKeyDown += (kds, kde) =>
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
                        _suggestionList.Items.MoveCurrentToPrevious();
                        if (_suggestionList.Items.CurrentItem == null)
                        {
                            _suggestionList.Items.MoveCurrentToPosition(_suggestionList.Items.Count - 1);
                        }
                        var i = _suggestionList.Items.CurrentPosition;
                        while (!((IntellisenseListItem)_suggestionList.Items.GetItemAt(Math.Abs(i) % _suggestionList.Items.Count)).IsEnabled)
                        {
                            i--;
                        }
                        _suggestionList.Items.MoveCurrentToPosition(Math.Abs(i) % _suggestionList.Items.Count);
                        ((IntellisenseListItem)_suggestionList.Items.CurrentItem).IsSelected = true;
                        ((IntellisenseListItem)_suggestionList.Items.CurrentItem).Focus();
                        kde.Handled = true;
                    }
                    else if (kde.Key == Key.Down)
                    {
                        _suggestionList.Items.MoveCurrentToNext();
                        if (_suggestionList.Items.CurrentItem == null)
                        {
                            _suggestionList.Items.MoveCurrentToPosition(0);
                        }
                        var i = _suggestionList.Items.CurrentPosition;
                        while (!((IntellisenseListItem)_suggestionList.Items.GetItemAt(Math.Abs(i) % _suggestionList.Items.Count)).IsEnabled)
                        {
                            i++;
                        }
                        _suggestionList.Items.MoveCurrentToPosition(Math.Abs(i) % _suggestionList.Items.Count);
                        ((IntellisenseListItem)_suggestionList.Items.CurrentItem).IsSelected = true;
                        ((IntellisenseListItem)_suggestionList.Items.CurrentItem).Focus();
                        kde.Handled = true;
                    }

                    // Execute the Intellisense item action
                    else if (kde.Key == Key.Enter || kde.Key == Key.Tab)
                    {
                        if (_suggestionList.Items.CurrentItem != null)
                        {
                            ((IntellisenseListItem)_suggestionList.Items.CurrentItem).Action();
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
                    if (SelectedText == string.Empty)
                    {
                        var lastCaretPos = CaretIndex;
                        Text = Text.Insert(lastCaretPos, ".");
                        CaretIndex = lastCaretPos + 1;
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
                        var lastCaretPos = CaretIndex;
                        Text = Text.Insert(lastCaretPos, ")");
                        CaretIndex = lastCaretPos;
                    }
                }
            };

            TextChanged += (s, e) =>
            {
                // Manually manage the Undo/Redo stack
                if (!_cancelNextStack)
                {
                    _stack.Push(new TextStack(Text, CaretIndex));
                }
                else
                {
                    _cancelNextStack = false;
                }

                // Filter the list
                FilterSuggestionList();

                UpdateTotalLineCount();
                InvalidateBlocks(e.Changes.First().Offset);
                InvalidateVisual();
            };

            PreviewKeyDown += (s, e) =>
            {
                var lastCaretPos = CaretIndex;

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
                        if (_suggestionList.Items.CurrentItem != null)
                        {
                            ((IntellisenseListItem)_suggestionList.Items.CurrentItem).Focus();
                        }
                        else
                        {
                            _suggestionList.Focus();
                        }
                        e.Handled = true;
                    }
                }

                // Shift Key + Tab (BackTab) 
                if (e.Key == Key.Tab && e.KeyboardDevice.Modifiers == ModifierKeys.Shift)
                {
                    if (SelectedText != string.Empty)
                    {
                        var lines = SelectedText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        for (var i = 0; i < lines.Length; i++)
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
                        SelectedText = string.Join(Environment.NewLine, lines);
                    }
                    else
                    {
                        var lastLine = Text.LastIndexOf(Environment.NewLine, lastCaretPos, StringComparison.Ordinal);

                        if (lastLine == -1)
                        {
                            lastLine = Text.Length - 1;
                        }

                        var startLine = Text.IndexOf(Environment.NewLine, lastLine, StringComparison.Ordinal);

                        if (startLine != -1)
                        {
                            startLine += Environment.NewLine.Length;
                        }
                        else
                        {
                            startLine = 0;
                        }

                        var spaces = 0;
                        for (var i = startLine; i < Text.Length - 1; i++)
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

                        Text = Text.Remove(startLine, spaces);

                        if (lastCaretPos >= startLine + spaces)
                        {
                            CaretIndex = lastCaretPos - spaces;
                        }
                        else if (lastCaretPos >= startLine && lastCaretPos < startLine + spaces)
                        {
                            CaretIndex = startLine;
                        }
                        else
                        {
                            CaretIndex = lastCaretPos;
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
                                if (_suggestionList.Items.CurrentItem != null)
                                {
                                    ((IntellisenseListItem)_suggestionList.Items.CurrentItem).Action();
                                }
                                else
                                {
                                    ((IntellisenseListItem)_suggestionList.Items[0]).Action();
                                }
                            }
                            else
                            {
                                HideSuggestionList();
                                Focus();
                            }
                        }
                        // Otherwise...
                        else if (SelectedText == string.Empty)
                        {
                            Text = Text.Insert(lastCaretPos, Tab);
                            CaretIndex = lastCaretPos + TabSize;
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

                        var lastLine = Text.LastIndexOf(Environment.NewLine, lastCaretPos, StringComparison.Ordinal);
                        var spaces = 0;

                        if (lastLine != -1)
                        {
                            var line = Text.Substring(lastLine, Text.Length - lastLine);

                            var startLine = line.IndexOf(Environment.NewLine, StringComparison.Ordinal);

                            if (startLine != -1)
                            {
                                line = line.Substring(startLine).TrimStart(Environment.NewLine.ToCharArray());
                            }

                            foreach (var c in line)
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
                        Text = Text.Insert(lastCaretPos, Environment.NewLine + new string(' ', spaces));
                        CaretIndex = lastCaretPos + Environment.NewLine.Length + spaces;

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
            _totalLineCount = TextUtilities.GetLineCount(Text);
        }

        private void UpdateBlocks()
        {
            if (_blocks.Count == 0)
            {
                return;
            }

            // While something is visible after last block...
            while (!_blocks.Last().IsLast && _blocks.Last().Position.Y + _blockHeight - VerticalOffset < ActualHeight)
            {
                var firstLineIndex = _blocks.Last().LineEndIndex + 1;
                var lastLineIndex = firstLineIndex + _maxLineCountInBlock - 1;
                lastLineIndex = lastLineIndex <= _totalLineCount - 1 ? lastLineIndex : _totalLineCount - 1;

                var firstCharIndex = _blocks.Last().CharEndIndex + 1;
                var lastCharIndex = TextUtilities.GetLastCharIndexFromLineIndex(Text, lastLineIndex);

                if (lastCharIndex <= firstCharIndex)
                {
                    _blocks.Last().IsLast = true;
                    return;
                }

                var block = new InnerTextBlock(
                    firstCharIndex,
                    lastCharIndex,
                    _blocks.Last().LineEndIndex + 1,
                    lastLineIndex,
                    LineHeight);
                block.RawText = block.GetSubString(Text);
                block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
                _blocks.Add(block);
                FormatBlock(block, _blocks.Count > 1 ? _blocks[_blocks.Count - 2] : null);
            }
        }

        private void InvalidateBlocks(int changeOffset)
        {
            InnerTextBlock blockChanged = null;
            for (var i = 0; i < _blocks.Count; i++)
            {
                if (_blocks[i].CharStartIndex <= changeOffset && changeOffset <= _blocks[i].CharEndIndex + 1)
                {
                    blockChanged = _blocks[i];
                    break;
                }
            }

            if (blockChanged == null && changeOffset > 0)
            {
                blockChanged = _blocks.Last();
            }

            var fvline = blockChanged?.LineStartIndex ?? 0;
            var lvline = GetIndexOfLastVisibleLine();
            var fvchar = blockChanged?.CharStartIndex ?? 0;
            var lvchar = TextUtilities.GetLastCharIndexFromLineIndex(Text, lvline);

            if (blockChanged != null)
            {
                _blocks.RemoveRange(_blocks.IndexOf(blockChanged), _blocks.Count - _blocks.IndexOf(blockChanged));
            }

            var localLineCount = 1;
            var charStart = fvchar;
            var lineStart = fvline;
            for (var i = fvchar; i < Text.Length; i++)
            {
                if (Text[i] == '\n')
                {
                    localLineCount += 1;
                }
                if (i == Text.Length - 1)
                {
                    var blockText = Text.Substring(charStart);
                    var block = new InnerTextBlock(
                        charStart,
                        i, lineStart,
                        lineStart + TextUtilities.GetLineCount(blockText) - 1,
                        LineHeight);
                    block.RawText = block.GetSubString(Text);
                    block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);
                    block.IsLast = true;

                    foreach (var b in _blocks)
                    {
                        if (b.LineStartIndex == block.LineStartIndex)
                        {
                            throw new Exception("Internal error occured.");
                        }
                    }

                    _blocks.Add(block);
                    FormatBlock(block, _blocks.Count > 1 ? _blocks[_blocks.Count - 2] : null);
                    break;
                }
                if (localLineCount > _maxLineCountInBlock)
                {
                    var block = new InnerTextBlock(
                        charStart,
                        i,
                        lineStart,
                        lineStart + _maxLineCountInBlock - 1,
                        LineHeight);
                    block.RawText = block.GetSubString(Text);
                    block.LineNumbers = GetFormattedLineNumbers(block.LineStartIndex, block.LineEndIndex);

                    foreach (var b in _blocks)
                    {
                        if (b.LineStartIndex == block.LineStartIndex)
                        {
                            throw new Exception("Internal error occured.");
                        }
                    }

                    _blocks.Add(block);
                    FormatBlock(block, _blocks.Count > 1 ? _blocks[_blocks.Count - 2] : null);

                    charStart = i + 1;
                    lineStart += _maxLineCountInBlock;
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
            if (!IsLoaded || _renderCanvas == null || _lineNumbersCanvas == null)
            {
                return;
            }

            var dc = _renderCanvas.GetContext();
            var dc2 = _lineNumbersCanvas.GetContext();
            for (var i = 0; i < _blocks.Count; i++)
            {
                var block = _blocks[i];
                var blockPos = block.Position;
                var top = blockPos.Y - VerticalOffset;
                var bottom = top + _blockHeight;
                if (top < ActualHeight && bottom > 0)
                {
                    try
                    {
                        dc.DrawText(block.FormattedText, new Point(2 - HorizontalOffset, block.Position.Y - VerticalOffset));
                        if (IsLineNumbersMarginVisible)
                        {
                            _lineNumbersCanvas.Width = GetFormattedTextWidth($"{_totalLineCount:0000}") + 5;
                            dc2.DrawText(block.LineNumbers, new Point(_lineNumbersCanvas.ActualWidth, 1 + block.Position.Y - VerticalOffset));
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
            if (_suggestionList.Items.Count > 0)
            {
                _suggestionCanvas.IsHitTestVisible = true;
                _suggestionList.Visibility = Visibility.Visible;
                _suggestionList.Focus();
            }
        }

        /// <summary>
        /// Add items to the Intellisense list.
        /// </summary>
        /// <param name="text">The text used to determine which item will be added.</param>
        /// <param name="pos">The position of the carret.</param>
        private void PopulateSuggestionList(string text, int? pos)
        {
            _suggestionList.Items.Clear();
            var leftCaretText = text ?? Text.Substring(0, CaretIndex);
            if (Regex.IsMatch(leftCaretText, "\\w+[^)\\\\]?[\\r\\n\\t ]*\\.$"))
            {
                var lastCaretPos = pos ?? CaretIndex;
                _suggestionList.Items.Add(new IntellisenseListItem("count", "count()", "Count the number of rows in a table.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "count()");
                    CaretIndex = lastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("delete", "delete()", "Remove the value of rows and columns in a table.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "delete()");
                    CaretIndex = lastCaretPos + 7;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("insert", "insert()", "Add a new row in a table.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "insert()");
                    CaretIndex = lastCaretPos + 7;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("max", "max()", "Return the higher value of all values in a column.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "max()");
                    CaretIndex = lastCaretPos + 4;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("min", "min()", "Return the lower value of all values in a column.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "min()");
                    CaretIndex = lastCaretPos + 4;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("update", "update()", "Update rows and columns with a new value.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "update()");
                    CaretIndex = lastCaretPos + 7;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("select", "select()", "Retrieve data from a table.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "select()");
                    CaretIndex = lastCaretPos + 7;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("sum", "sum()", "Return the sum of all values in a column.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "sum()");
                    CaretIndex = lastCaretPos + 4;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("truncate", "truncate()", "Delete all data and reset the table.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "truncate()");
                    CaretIndex = lastCaretPos + 9;
                    Focus();
                    HideSuggestionList();
                }));
            }
            else if (Regex.IsMatch(leftCaretText, "\\)[\\r\\n\\t ]*\\.$"))
            {
                var lastCaretPos = pos ?? CaretIndex;
                _suggestionList.Items.Add(new IntellisenseListItem("as", "as()", "Create an alias for a query parameter. Use it with select() and count().", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "as()");
                    CaretIndex = lastCaretPos + 3;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("group", "group()", "Group the number of value's occurences. Use it with count().", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "group()");
                    CaretIndex = lastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("in", "in()", "Define in which columns we have to insert data. Use it with insert().", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "in()");
                    CaretIndex = lastCaretPos + 3;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("limit", "limit()", "Limit the number of retrieved results. Use it with select().", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "limit()");
                    CaretIndex = lastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("on", "on()", "Define which action to execute on a column. Use it with select().", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "on()");
                    CaretIndex = lastCaretPos + 3;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("order", "order()", "Reorder retrieved results. Use it with select().", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "order()");
                    CaretIndex = lastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("where", "where()", "Apply conditions to the query.", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "where()");
                    CaretIndex = lastCaretPos + 6;
                    Focus();
                    HideSuggestionList();
                }));
                _suggestionList.Items.Add(new IntellisenseListItem("with", "with()", "Define which values to use. Use it with update().", () =>
                {
                    if (CaretIndex - lastCaretPos > 0)
                    {
                        Text = Text.Remove(lastCaretPos, CaretIndex - lastCaretPos);
                    }
                    Text = Text.Insert(lastCaretPos, "with()");
                    CaretIndex = lastCaretPos + 5;
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
            var position = GetRectFromCharacterIndex(CaretIndex).BottomRight;

            var left = position.X - _lineNumbersCanvas.ActualWidth - _lineNumbersCanvas.Margin.Left - _lineNumbersCanvas.Margin.Right - _lineNumbersSeparator.Margin.Left - _lineNumbersSeparator.Margin.Right - Padding.Left - Margin.Left - Padding.Right - Margin.Right;
            var top = position.Y - Padding.Top;

            if (left + _suggestionList.ActualWidth > _suggestionCanvas.ActualWidth)
            {
                left = _suggestionCanvas.ActualWidth - _suggestionList.ActualWidth - Padding.Right - Margin.Right;
            }

            if (top + _suggestionList.ActualHeight > _suggestionCanvas.ActualHeight)
            {
                top = _suggestionCanvas.ActualHeight - _suggestionList.ActualHeight - Padding.Bottom - Margin.Bottom;
            }

            Canvas.SetLeft(_suggestionList, left);
            Canvas.SetTop(_suggestionList, top);
        }

        /// <summary>
        /// Hide suggestion list
        /// </summary>
        private void HideSuggestionList()
        {
            _suggestionCanvas.IsHitTestVisible = false;
            _suggestionList.Visibility = Visibility.Collapsed;
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
            var lastCaretPos = 0;
            if (saveCaretPosition)
            {
                lastCaretPos = CaretIndex;
            }
            Text = text;
            CaretIndex = lastCaretPos;
        }

        /// <summary>
        /// Clean the current document.
        /// </summary>
        public void CleanDocument()
        {
            Text = string.Empty;
        }

        /// <summary>
        /// Check if the user can do an undo operation.
        /// </summary>
        /// <returns>true if it's possible, and false otherwise.</returns>
        public new bool CanUndo()
        {
            return _stack.UndoCount > 0;
        }

        /// <summary>
        /// Check if the user can do a redo operation.
        /// </summary>
        /// <returns>true if it's possible, and false otherwise.</returns>
        public new bool CanRedo()
        {
            return _stack.RedoCount > 0;
        }

        /// <summary>
        /// Undo an operation in the stack.
        /// </summary>
        public new void Undo()
        {
            HideSuggestionList();
            var thisStack = _stack.UnPush(new TextStack(Text, CaretIndex));
            if (thisStack != null)
            {
                _cancelNextStack = true;
                var state = thisStack.Do(((TextStack)thisStack).State);
                Text = state.Text;
                CaretIndex = state.CaretIndex;
            }
        }

        /// <summary>
        /// Redo an operation in the stack.
        /// </summary>
        public new void Redo()
        {
            HideSuggestionList();
            var thisStack = _stack.RePush(new TextStack(Text, CaretIndex));
            if (thisStack != null)
            {
                _cancelNextStack = true;
                var state = thisStack.Do(((TextStack)thisStack).State);
                Text = state.Text;
                CaretIndex = state.CaretIndex;
            }
        }

        /// <summary>
        /// Reset the undo/redo stack.
        /// </summary>
        public void ResetUndoRedoStack()
        {
            _stack.Reset();
        }

        /// <summary>
        /// Filter the Intellisense list to display only items who matches the text entered.
        /// </summary>
        public void FilterSuggestionList()
        {
            if (SuggestionListIsVisible())
            {
                var activeLineIndex = GetIndexOfActiveLine() - 1;
                var firstCharIndex = TextUtilities.GetFirstCharIndexFromLineIndex(Text, activeLineIndex);
                var lastCharIndex = TextUtilities.GetLastCharIndexFromLineIndex(Text, activeLineIndex);

                var lineString = Text.Substring(firstCharIndex, lastCharIndex - firstCharIndex);
                var lastDotPosition = lineString.LastIndexOf('.');

                var suggestionLabelPart = lineString.Substring(lastDotPosition + 1);

                // Refresh the list first
                for (int i = 0, l = _suggestionList.Items.Count; i < l; i++)
                {
                    var item = (IntellisenseListItem)_suggestionList.Items[i];
                    item.Visibility = Visibility.Visible;
                    item.IsEnabled = true;
                }

                var h = 0;
                for (int i = 0, l = _suggestionList.Items.Count; i < l; i++)
                {
                    var item = (IntellisenseListItem)_suggestionList.Items[i];
                    if (!Regex.IsMatch(item.DisplayText, "^" + Regex.Escape(suggestionLabelPart)))
                    {
                        item.Visibility = Visibility.Collapsed;
                        item.IsEnabled = false;
                        h++;
                    }
                    else
                    {
                        item.Focus();
                    }
                }

                // If all items are hidden
                if (h == _suggestionList.Items.Count)
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
            return _suggestionList.Visibility == Visibility.Visible;
        }

        /// <summary>
        /// Check if the suggestion list have at least one item.
        /// </summary>
        /// <returns></returns>
        public bool SuggestionListHasItems()
        {
            return _suggestionList.Items.Count > 0;
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
            var guessedLine = (int)(VerticalOffset / _lineHeight);
            return guessedLine > _totalLineCount ? _totalLineCount : guessedLine;
        }

        /// <summary>
        /// Returns the index of the last visible text line.
        /// </summary>
        public int GetIndexOfLastVisibleLine()
        {
            var height = VerticalOffset + ViewportHeight;
            var guessedLine = (int)(height / _lineHeight);
            return guessedLine > _totalLineCount - 1 ? _totalLineCount - 1 : guessedLine;
        }

        /// <summary>
        /// Formats and Highlights the text of a block.
        /// </summary>
        private void FormatBlock(InnerTextBlock currentBlock, InnerTextBlock previousBlock)
        {
            currentBlock.FormattedText = GetFormattedText(currentBlock.RawText);
            if (CurrentHighlighter != null)
            {
                var previousCode = previousBlock?.Code ?? -1;
                currentBlock.Code = CurrentHighlighter.HighlightBlock(currentBlock.FormattedText, previousCode);
            }
        }

        /// <summary>
        /// Returns a formatted text object from the given string
        /// </summary>
        private FormattedText GetFormattedText(string text)
        {
            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize,
                TextColor)
            {
                Trimming = TextTrimming.None,
                LineHeight = _lineHeight
            };


            return ft;
        }

        /// <summary>
        /// Returns a string containing a list of numbers separated with newlines.
        /// </summary>
        private FormattedText GetFormattedLineNumbers(int firstIndex, int lastIndex)
        {
            var text = "";
            for (var i = firstIndex + 1; i <= lastIndex + 1; i++)
            {
                text += i.ToString() + Environment.NewLine;
            }
            text = text.Trim();

            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize,
                new SolidColorBrush(Color.FromRgb(0x21, 0xA1, 0xD8)))
            {
                Trimming = TextTrimming.None,
                LineHeight = _lineHeight,
                TextAlignment = TextAlignment.Right
            };


            return ft;
        }

        /// <summary>
        /// Returns the width of a text once formatted.
        /// </summary>
        private double GetFormattedTextWidth(string text)
        {
            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize,
                TextColor)
            {
                Trimming = TextTrimming.None,
                LineHeight = _lineHeight
            };


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
            var activeLineIndex = GetIndexOfActiveLine();
            var firstCharIndex = TextUtilities.GetFirstCharIndexFromLineIndex(Text, activeLineIndex - 1);
            var colIndex = Text.Substring(firstCharIndex, CaretIndex - firstCharIndex).Length + 1;
            var charNb = Text.Length;

            return $"Ln {activeLineIndex}    Col {colIndex}    Ch {CaretIndex}/{charNb}";
        }

        // ----------------------------------------------------------
        // Dependency Properties
        // ----------------------------------------------------------

        public static readonly DependencyProperty IsLineNumbersMarginVisibleProperty = DependencyProperty.Register(
            "IsLineNumbersMarginVisible", typeof(bool), typeof(SyntaxHighlightBox), new PropertyMetadata(true));

        public static readonly DependencyProperty TextColorProperty = DependencyProperty.Register(
            "TextColor", typeof(Brush), typeof(SyntaxHighlightBox), new PropertyMetadata(Brushes.Black));

        public static readonly DependencyProperty LineNumbersBackgroundColorProperty = DependencyProperty.Register(
            "LineNumbersBackgroundColor", typeof(Brush), typeof(SyntaxHighlightBox), new PropertyMetadata(Brushes.Transparent));

        public static readonly DependencyProperty TextEditorBackgroundColorProperty = DependencyProperty.Register(
            "TextEditorBackgroundColor", typeof(Brush), typeof(SyntaxHighlightBox), new PropertyMetadata(Brushes.Transparent));

        // ----------------------------------------------------------
        // Properties
        // ----------------------------------------------------------

        public int TabSize => 4;

        private string Tab => new string(' ', TabSize);

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

        public Brush LineNumbersBackgroundColor
        {
            get { return (Brush)GetValue(LineNumbersBackgroundColorProperty); }
            set { SetValue(LineNumbersBackgroundColorProperty, value); }
        }

        public Brush TextEditorBackgroundColor
        {
            get { return (Brush)GetValue(TextEditorBackgroundColorProperty); }
            set { SetValue(TextEditorBackgroundColorProperty, value); }
        }

        // ----------------------------------------------------------
        // Structs
        // ----------------------------------------------------------

        private struct TextState
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
            public Point Position => new Point(0, LineStartIndex * _lineHeight);
            public bool IsLast { get; set; }
            public int Code { get; set; }

            private readonly double _lineHeight;

            public InnerTextBlock(int charStart, int charEnd, int lineStart, int lineEnd, double lineHeight)
            {
                CharStartIndex = charStart;
                CharEndIndex = charEnd;
                LineStartIndex = lineStart;
                LineEndIndex = lineEnd;
                _lineHeight = lineHeight;
                IsLast = false;

            }

            public string GetSubString(string text)
            {
                var length = CharEndIndex < text.Length ? CharEndIndex - CharStartIndex + 1 : CharEndIndex - CharStartIndex;
                return text.Substring(CharStartIndex, length);
            }

            public override string ToString()
            {
                return $"L:{LineStartIndex}/{LineEndIndex} C:{CharStartIndex}/{CharEndIndex} {FormattedText.Text}";
            }
        }

        private class TextStack : IStack<TextState>
        {
            public TextState State { get; }

            public TextStack()
            {
                State = new TextState
                {
                    Text = string.Empty,
                    CaretIndex = 0
                };
            }

            public TextStack(string text, int caret)
            {
                State = new TextState
                {
                    CaretIndex = caret,
                    Text = text
                };
            }

            public TextState Do(TextState now)
            {
                return now;
            }

            public TextState Undo(TextState last)
            {
                return State;
            }
        }
    }
}
