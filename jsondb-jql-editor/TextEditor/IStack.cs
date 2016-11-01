namespace JSONDB.JQLEditor.TextEditor
{
    public interface IStack<T>
    {
        T Do(T input);
        T Undo(T input);
    }
}
