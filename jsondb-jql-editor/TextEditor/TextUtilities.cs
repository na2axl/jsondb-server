using System;
using System.Text.RegularExpressions;

namespace JSONDB.JQLEditor.TextEditor
{
    public class TextUtilities
    {
        /// <summary>
        /// Returns the raw number of the current line count.
        /// </summary>
        public static int GetLineCount(string text)
        {
            int lcnt = 1;
            lcnt = Math.Max(lcnt, Regex.Split(text, Environment.NewLine).Length);
            return lcnt;
        }

        /// <summary>
        /// Returns the index of the first character of the
        /// specified line. If the index is greater than the current
        /// line count, the method returns the index of the last
        /// character. The line index is zero-based.
        /// </summary>
        public static int GetFirstCharIndexFromLineIndex(string text, int lineIndex)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }
            if (lineIndex <= 0)
            {
                return 0;
            }

            return GetLastCharIndexFromLineIndex(text, lineIndex - 1) + Environment.NewLine.Length;
        }

        /// <summary>
        /// Returns the index of the last character of the
        /// specified line. If the index is greater than the current
        /// line count, the method returns the index of the last
        /// character. The line-index is zero-based.
        /// </summary>
        public static int GetLastCharIndexFromLineIndex(string text, int lineIndex)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }
            if (lineIndex < 0)
            {
                return 0;
            }

            string[] lines = Regex.Split(text, Environment.NewLine);
            int lastIndex = 0;
            for (int i = 0; i <= lines.Length - 1; i++)
            {
                lastIndex += lines[i].Length + Environment.NewLine.Length;
                if (lineIndex == i)
                {
                    break;
                }
            }

            return lastIndex - Environment.NewLine.Length;
        }
    }
}
