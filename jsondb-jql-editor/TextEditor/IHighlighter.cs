﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace JSONDB.JQLEditor.TextEditor
{
    public interface IHighlighter
    {
        /// <summary>
        /// Highlights the text of the current block.
        /// </summary>
        /// <param name="text">The text from the current block to highlight</param>
        /// <param name="previousBlockCode">The code assigned to the previous block, or -1 if
        /// there is no previous block</param>
        /// <returns>The current block code</returns>
        int HighlightBlock(FormattedText text, int previousBlockCode);

        /// <summary>
        /// Just Highlight the text.
        /// </summary>
        /// <param name="text">The text to highlight</param>
        /// <returns>The highlighted</returns>
        FormattedText Highlight(FormattedText text);
    }
}
