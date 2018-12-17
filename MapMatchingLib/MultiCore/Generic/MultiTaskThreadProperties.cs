using MapMatchingLib.SysTools;

namespace MapMatchingLib.MultiCore.Generic
{
    public abstract class MultiTaskThreadProperties
    {
        protected MultiTaskThreadProperties(long startPos, long endPos)
        {
            StartPos = startPos;
            EndPos = endPos;
        }
        public int ThreadId { get; set; }
        public Counter SubmitCount { get; set; }
        public long StartPos { get; set; }
        public long EndPos { get; set; }
    }

    public class ListTraversalThreadProperties : MultiTaskThreadProperties
    {
        public ListTraversalThreadProperties(long startPos, long endPos)
            : base(startPos, endPos)
        {
        }
    }

    public class ReaderThreadProperties : MultiTaskThreadProperties
    {
        public string InputFile { get; set; }

        public ReaderThreadProperties(long startPos, long endPos, string inputFile) : base(startPos, endPos)
        {
            InputFile = inputFile;
        }
    }

    public class WriterThreadProperties : ReaderThreadProperties
    {
        public string OutputFile { get; set; }

        public WriterThreadProperties(long startPos, long endPos, string inputFile, string outputFile)
            : base(startPos, endPos, inputFile)
        {
            OutputFile = outputFile;
        }
    }
}