using System;
using System.Collections.Generic;
using System.Threading;
using MatchingService.MultiCore.Generic;
using MatchingService.SysTools;

namespace MatchingService.MultiCore
{
    public abstract class MultiTaskWorkManager
    {
        protected readonly IProgressReporter Reporter;
        protected Counter TaskCount = new Counter();
        protected int CoreNumber;
        private DateTime _startTime, _currentime;
        //private WaitCallback _workThread;
        private readonly MultiTaskCallbackFunc _callBack;
        private readonly MultiTaskWorkingFunc _workFunc;
        private long _currentWork, _totalWork;
        private Counter[] _submitCounts;

        protected MultiTaskWorkManager(IProgressReporter reporter, int coreNumber, MultiTaskWorkingFunc workFunc, MultiTaskCallbackFunc callBack)
        {
            Reporter = reporter;
            CoreNumber = coreNumber;
            _workFunc = workFunc;
            _callBack = callBack;
            MonitorInverval = 3000;
        }
        public int MonitorInverval { get; set; }

        /// <summary>
        /// Start the work manager.
        /// </summary>
        public void StartWork()
        {
            ThreadPool.QueueUserWorkItem(WorkManager);
        }

        /// <summary>
        /// Prepare work set and return total work as long.
        /// </summary>
        /// <returns>Total work count using long</returns>
        protected abstract long GetWorkSize();

        /// <summary>
        /// Split work set and return parameters for every thread.
        /// </summary>
        /// <returns></returns>
        protected abstract List<MultiTaskThreadProperties> SplitWork();
        
        private void WorkManager(object o)
        {
            _submitCounts = new Counter[CoreNumber];
            _totalWork = GetWorkSize();
            for (int i = 0; i < CoreNumber; i++)
            {
                _submitCounts[i] = new Counter();
            }
            List<MultiTaskThreadProperties> parameters = SplitWork();
            QueueEachWork(parameters);
            while (TaskCount.Value > 0)
            {
                _currentime = DateTime.Now;
                _currentWork = 0;
                for (int i = 0; i < CoreNumber; i++)
                {
                    lock (_submitCounts[i])
                    {
                        _currentWork += _submitCounts[i].Value;
                    }
                }
                if (_currentWork > 0)
                {
                    float progress = _currentWork / (float)_totalWork;
                    Reporter.UpdateStatus(progress, _startTime.AddSeconds((_currentime - _startTime).TotalSeconds / progress));
                }
                Thread.Sleep(MonitorInverval);
            }
            Reporter.UpdateStatus(1, DateTime.Now);
            Reporter.TaskComplete();
            _callBack();
        }

        /// <summary>
        /// Start each thread for each work item.
        /// </summary>
        /// <param name="parameters">The parameters come from SplitWork function</param>
        private void QueueEachWork(IList<MultiTaskThreadProperties> parameters)
        {
            _startTime = DateTime.Now;
            for (int i = 0; i < CoreNumber; i++)
            {
                lock (TaskCount)
                {
                    TaskCount.Increase();
                }
                parameters[i].ThreadId = i;
                parameters[i].SubmitCount = _submitCounts[i];
                ThreadPool.QueueUserWorkItem(WorkThread, parameters[i]);
            }
        }

        private void WorkThread(object o)
        {
            _workFunc(o);
            lock (TaskCount)
            {
                TaskCount.Decrease();
            }
        }
    }
}