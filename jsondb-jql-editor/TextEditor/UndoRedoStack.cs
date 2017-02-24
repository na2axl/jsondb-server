using System.Collections.Generic;

namespace JSONDB.JQLEditor.TextEditor
{
    public class UndoRedoStack<T>
    {
        private Stack<IStack<T>> _undo;
        private Stack<IStack<T>> _redo;

        public int UndoCount
        {
            get
            {
                return _undo.Count;
            }
        }

        public int RedoCount
        {
            get
            {
                return _redo.Count;
            }
        }

        public UndoRedoStack()
        {
            Reset();
        }

        public void Reset()
        {
            _undo = new Stack<IStack<T>>();
            _redo = new Stack<IStack<T>>();
        }

        public T Do(IStack<T> cmd, T input)
        {
            var output = cmd.Do(input);
            _undo.Push(cmd);
            _redo.Clear();
            return output;
        }

        public T Undo(T input)
        {
            if (_undo.Count > 0)
            {
                IStack<T> cmd = _undo.Pop();
                T output = cmd.Undo(input);
                _redo.Push(cmd);
                return output;
            }
            return input;
        }

        public T Redo(T input)
        {
            if (_redo.Count > 0)
            {
                IStack<T> cmd = _redo.Pop();
                T output = cmd.Do(input);
                _undo.Push(cmd);
                return output;
            }
            return input;
        }

        public void Push(IStack<T> cmd)
        {
            _undo.Push(cmd);
            _redo.Clear();
        }

        public IStack<T> UnPush(IStack<T> now)
        {
            if (_undo.Count > 0)
            {
                IStack<T> cmd = _undo.Pop();
                _redo.Push(now);
                return cmd;
            }
            return null;
        }

        public IStack<T> RePush(IStack<T> now)
        {
            if (_redo.Count > 0)
            {
                IStack<T> cmd = _redo.Pop();
                _undo.Push(now);
                return cmd;
            }
            return null;
        }
    }
}
