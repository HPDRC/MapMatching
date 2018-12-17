using System.Collections.Generic;
using MatchingService.MultiCore.Generic;

namespace MatchingService.MultiCore
{
    public class SingleThreadWorkManager : MultiTaskWorkManager
    {
        
        private readonly int _workSize;
        public SingleThreadWorkManager(IProgressReporter reporter, MultiTaskWorkingFunc workFunc, MultiTaskCallbackFunc callBack, int workSize)
            : base(reporter, 1, workFunc, callBack)
        {
            _workSize = workSize;
        }

        protected override long GetWorkSize()
        {
            return _workSize;//6010490L;
        }

        protected override List<MultiTaskThreadProperties> SplitWork()
        {
            List<MultiTaskThreadProperties> param = new List<MultiTaskThreadProperties>
            {
                new ListTraversalThreadProperties(0, GetWorkSize())
            };
            return param;
        }
    }
}