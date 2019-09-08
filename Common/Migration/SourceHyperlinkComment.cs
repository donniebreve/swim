using System.Collections.Generic;

namespace Common
{
    public class SourceHyperlinkComment
    {
        public int SourceRev { get; set; }

        // I do not think this is necessary
        //public IList<string> MigrationActions { get; set; }

        public SourceHyperlinkComment() { }

        public SourceHyperlinkComment(int sourceRev)
        {
            this.SourceRev = sourceRev;
        }
    }
}
