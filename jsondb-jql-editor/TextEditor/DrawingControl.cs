using System;
using System.Windows;
using System.Windows.Media;

namespace JSONDB.JQLEditor.TextEditor
{
    public class DrawingControl : FrameworkElement
    {
        private VisualCollection _visuals;
        private DrawingVisual _visual;

        public DrawingControl()
        {
            _visual = new DrawingVisual();
            _visuals = new VisualCollection(this);
            _visuals.Add(_visual);
        }

        public DrawingContext GetContext()
        {
            return _visual.RenderOpen();
        }

        protected override int VisualChildrenCount
        {
            get { return _visuals.Count; }
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _visuals.Count)
            {
                throw new ArgumentOutOfRangeException();
            }
            return _visuals[index];
        }
    }
}
