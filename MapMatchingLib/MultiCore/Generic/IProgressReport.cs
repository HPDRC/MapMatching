using System;

namespace MapMatchingLib.MultiCore.Generic
{
    public interface IProgressReporter
    {
        void CleanStatus();
        void UpdateStatus(float progress, DateTime estimation);
        void TaskComplete();
    }
}