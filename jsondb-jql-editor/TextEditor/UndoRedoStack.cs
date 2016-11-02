using System.Collections.Generic;

namespace JSONDB.JQLEditor.TextEditor
{
    public class UndoRedoStack<T>
    {
        private Stack<IStack<T>> _Undo;
        private Stack<IStack<T>> _Redo;

        public int UndoCount
        {
            get
            {
                return _Undo.Count;
            }
        }

        public int RedoCount
        {
            get
            {
                return _Redo.Count;
            }
        }

        public UndoRedoStack()
        {
            Reset();
        }

        public void Reset()
        {
            _Undo = new Stack<IStack<T>>();
            _Redo = new Stack<IStack<T>>();
        }

        public T Do(IStack<T> cmd, T input)
        {
            T output = cmd.Do(input);
            _Undo.Push(cmd);
            _Redo.Clear();
            return output;
        }

        public T Undo(T input)
        {
            if (_Undo.Count > 0)
            {
                IStack<T> cmd = _Undo.Pop();
                T output = cmd.Undo(input);
                _Redo.Push(cmd);
                return output;
            }
            else
            {
                return input;
            }
        }

        public T Redo(T input)
        {
            if (_Redo.Count > 0)
            {
                IStack<T> cmd = _Redo.Pop();
                T output = cmd.Do(input);
                _Undo.Push(cmd);
                return output;
            }
            else
            {
                return input;
            }
        }

        public void Push(IStack<T> cmd)
        {
            _Undo.Push(cmd);
            _Redo.Clear();
        }

        public IStack<T> UnPush(IStack<T> now)
        {
            if (_Undo.Count > 0)
            {
                IStack<T> cmd = _Undo.Pop();
                _Redo.Push(now);
                return cmd;
            }
            else
            {
                return null;
            }
        }

        public IStack<T> RePush(IStack<T> now)
        {
            if (_Redo.Count > 0)
            {
                IStack<T> cmd = _Redo.Pop();
                _Undo.Push(now);
                return cmd;
            }
            else
            {
                return null;
            }
        }
    }
}
