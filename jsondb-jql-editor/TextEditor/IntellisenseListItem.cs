using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JSONDB.JQLEditor.TextEditor
{
    public class IntellisenseListItem : ListBoxItem
    {
        private Canvas _toolTipMessage = new Canvas();
        private Border _toolTipBorder = new Border();
        private DockPanel _toolTipContainer = new DockPanel();
        private Label _toolTipTitle = new Label();
        private Separator _toolTipSeparator = new Separator();
        private TextBlock _toolTipDescription = new TextBlock();

        public Action Action { get; set; }
        public string DisplayText
        {
            get { return Content.ToString(); }
            set { Content = value; }
        }

        public IntellisenseListItem(string displayText, string helpTitle, string helpDescription, Action action)
        {
            Content = displayText;
            this.Action = action;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    action();
                    e.Handled = true;
                }
            };

            PreviewMouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    action();
                    e.Handled = true;
                }
            };

            _toolTipBorder.Padding = new Thickness(10, 5, 10, 5);
            _toolTipBorder.BorderBrush = (Brush)(new BrushConverter().ConvertFrom("#555555"));
            _toolTipBorder.Background = (Brush)(new BrushConverter().ConvertFrom("#333333"));

            _toolTipTitle.Content = helpTitle;
            _toolTipTitle.FontWeight = FontWeights.Bold;
            _toolTipTitle.Margin = new Thickness(0, 0, 0, 5);
            _toolTipTitle.Padding = new Thickness(0);
            _toolTipTitle.Foreground = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));

            _toolTipSeparator.Foreground = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));
            _toolTipSeparator.Height = 1;

            _toolTipDescription.Text = helpDescription;
            _toolTipDescription.TextWrapping = TextWrapping.Wrap;
            _toolTipDescription.MaxWidth = 200;
            _toolTipDescription.Margin = new Thickness(0);
            _toolTipDescription.Padding = new Thickness(0);
            _toolTipDescription.Foreground = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));

            _toolTipContainer.Children.Add(_toolTipTitle);
            _toolTipContainer.Children.Add(_toolTipSeparator);
            _toolTipContainer.Children.Add(_toolTipDescription);

            DockPanel.SetDock(_toolTipTitle, Dock.Top);
            DockPanel.SetDock(_toolTipSeparator, Dock.Top);
            DockPanel.SetDock(_toolTipDescription, Dock.Top);

            _toolTipBorder.Child = _toolTipContainer;

            _toolTipMessage.Children.Add(_toolTipBorder);
            _toolTipMessage.IsHitTestVisible = false;
            _toolTipBorder.Visibility = Visibility.Collapsed;

            Loaded += (s, e) =>
            {
                var parent = (Grid)((Canvas)((ListBox)Parent).Parent).Parent;
                parent.Children.Add(_toolTipMessage);
                UpdatePosition();
            };

            GotFocus += (s, e) =>
            {
                UpdatePosition();
                _toolTipMessage.IsHitTestVisible = true;
                _toolTipBorder.Visibility = Visibility.Visible;
            };

            LostFocus += (s, e) =>
            {
                UpdatePosition();
                _toolTipMessage.IsHitTestVisible = false;
                _toolTipBorder.Visibility = Visibility.Collapsed;
            };
        }

        public void UpdatePosition()
        {
            _toolTipBorder.BorderThickness = new Thickness(5, 0, 0, 0);

            _toolTipTitle.HorizontalContentAlignment = HorizontalAlignment.Left;
            _toolTipDescription.TextAlignment = TextAlignment.Left;

            var list = ((ListBox)Parent);
            var top = Canvas.GetTop(list);
            var left = Canvas.GetLeft(list) + list.ActualWidth + 5;

            if (left + _toolTipContainer.ActualWidth > _toolTipMessage.ActualWidth)
            {
                _toolTipBorder.BorderThickness = new Thickness(0, 0, 5, 0);
                _toolTipTitle.HorizontalContentAlignment = HorizontalAlignment.Right;
                _toolTipDescription.TextAlignment = TextAlignment.Right;
                left = Canvas.GetLeft(list) - _toolTipContainer.ActualWidth - _toolTipBorder.Padding.Left - _toolTipBorder.Padding.Right - _toolTipBorder.BorderThickness.Left - _toolTipBorder.BorderThickness.Right - 5;
            }

            if (top + _toolTipContainer.ActualHeight > _toolTipMessage.ActualHeight)
            {
                top = top - _toolTipBorder.ActualHeight - 5;
            }

            Canvas.SetTop(_toolTipBorder, top);
            Canvas.SetLeft(_toolTipBorder, left);
        }

    }
}
