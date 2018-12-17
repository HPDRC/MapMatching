using System;

namespace MatchingService.MultiCore.Generic
{
    public interface IProgressReporter
    {
        void CleanStatus();
        void UpdateStatus(float progress, DateTime estimation);
        void TaskComplete();
    }
}