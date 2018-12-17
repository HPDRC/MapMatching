namespace MatchingService.MultiCore.Generic
{
    public interface ILogger
    {
        void WriteLog(string log);
        void WriteStats(string log);
        //double GetSigma();
        //double GetBeta();
        //void SetSigma(double sigma);
        //void SetBeta(double beta);
        //double GetDiffPercent();
        //double GetDistance();
        //double GetInterval();
    }
}
