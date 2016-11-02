using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace JSONDB.JQLEditor.TextEditor
{
    public class IntellisenseListItem : ListBoxItem
    {
        private Canvas ToolTipMessage = new Canvas();
        private Border ToolTipBorder = new Border();
        private DockPanel ToolTipContainer = new DockPanel();
        private Label ToolTipTitle = new Label();
        private Separator ToolTipSeparator = new Separator();
        private TextBlock ToolTipDescription = new TextBlock();

        public Action Action { get; set; }
        public string DisplayText
        {
            get { return Content.ToString(); }
            set { Content = value; }
        }

        public IntellisenseListItem(string DisplayText, string HelpTitle, string HelpDescription, Action Action)
        {
            Content = DisplayText;
            this.Action = Action;

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Tab)
                {
                    Action();
                    e.Handled = true;
                }
            };

            PreviewMouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Action();
                    e.Handled = true;
                }
            };

            ToolTipBorder.Padding = new Thickness(10, 5, 10, 5);
            ToolTipBorder.BorderBrush = (Brush)(new BrushConverter().ConvertFrom("#555555"));
            ToolTipBorder.Background = (Brush)(new BrushConverter().ConvertFrom("#333333"));

            ToolTipTitle.Content = HelpTitle;
            ToolTipTitle.FontWeight = FontWeights.Bold;
            ToolTipTitle.Margin = new Thickness(0, 0, 0, 5);
            ToolTipTitle.Padding = new Thickness(0);
            ToolTipTitle.Foreground = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));

            ToolTipSeparator.Foreground = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));
            ToolTipSeparator.Height = 1;

            ToolTipDescription.Text = HelpDescription;
            ToolTipDescription.TextWrapping = TextWrapping.Wrap;
            ToolTipDescription.MaxWidth = 200;
            ToolTipDescription.Margin = new Thickness(0);
            ToolTipDescription.Padding = new Thickness(0);
            ToolTipDescription.Foreground = (Brush)(new BrushConverter().ConvertFrom("#ffffff"));

            ToolTipContainer.Children.Add(ToolTipTitle);
            ToolTipContainer.Children.Add(ToolTipSeparator);
            ToolTipContainer.Children.Add(ToolTipDescription);

            DockPanel.SetDock(ToolTipTitle, Dock.Top);
            DockPanel.SetDock(ToolTipSeparator, Dock.Top);
            DockPanel.SetDock(ToolTipDescription, Dock.Top);

            ToolTipBorder.Child = ToolTipContainer;

            ToolTipMessage.Children.Add(ToolTipBorder);
            ToolTipMessage.IsHitTestVisible = false;
            ToolTipBorder.Visibility = Visibility.Collapsed;

            Loaded += (s, e) =>
            {
                Grid parent = (Grid)((Canvas)((ListBox)Parent).Parent).Parent;
                parent.Children.Add(ToolTipMessage);
                UpdatePosition();
            };

            GotFocus += (s, e) =>
            {
                UpdatePosition();
                ToolTipMessage.IsHitTestVisible = true;
                ToolTipBorder.Visibility = Visibility.Visible;
            };

            LostFocus += (s, e) =>
            {
                UpdatePosition();
                ToolTipMessage.IsHitTestVisible = false;
                ToolTipBorder.Visibility = Visibility.Collapsed;
            };
        }

        public void UpdatePosition()
        {
            ToolTipBorder.BorderThickness = new Thickness(5, 0, 0, 0);

            ToolTipTitle.HorizontalContentAlignment = HorizontalAlignment.Left;
            ToolTipDescription.TextAlignment = TextAlignment.Left;

            ListBox list = ((ListBox)Parent);
            double top = Canvas.GetTop(list);
            double left = Canvas.GetLeft(list) + list.ActualWidth + 5;

            if (left + ToolTipContainer.ActualWidth > ToolTipMessage.ActualWidth)
            {
                ToolTipBorder.BorderThickness = new Thickness(0, 0, 5, 0);
                ToolTipTitle.HorizontalContentAlignment = HorizontalAlignment.Right;
                ToolTipDescription.TextAlignment = TextAlignment.Right;
                left = Canvas.GetLeft(list) - ToolTipContainer.ActualWidth - ToolTipBorder.Padding.Left - ToolTipBorder.Padding.Right - ToolTipBorder.BorderThickness.Left - ToolTipBorder.BorderThickness.Right - 5;
            }

            if (top + ToolTipContainer.ActualHeight > ToolTipMessage.ActualHeight)
            {
                top = top - ToolTipBorder.ActualHeight - 5;
            }

            Canvas.SetTop(ToolTipBorder, top);
            Canvas.SetLeft(ToolTipBorder, left);
        }

    }
}
