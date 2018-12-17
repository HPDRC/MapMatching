using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MapMatchingLib.Astar;
using MapMatchingLib.MapMatching;
using MapMatchingLib.MultiCore.Generic;
using MapMatchingLib.Routing;
using MapMatchingLib.RTree;
using MapMatchingLib.SysTools;
using MapMatchingLib.Tour;

namespace MapMatchingService
{
    public partial class MatchingService : ServiceBase,ILogger,IProgressReporter
    {
        public MatchingService()
        {
            InitializeComponent();
        }

        private RoutingMachine _routing;
        private GraphLoader _loader;
        protected override void OnStart(string[] args)
        {
            try
            {
                _loader = new GraphLoader(this, this, GraphLoaded, "miami");
                _loader.LoadGraph();
            }
            catch (Exception ex)
            {
                WriteLog(ex.ToString());
            }
        }

        private void GraphLoaded()
        {
            _routing = _loader.RMachine;
            Thread server1 = new Thread(RoutingThread) {IsBackground = true};
            server1.Start();

            Thread server2 = new Thread(KNNThread) {IsBackground = true};
            server2.Start();

            Thread server3 = new Thread(MatchingThread) { IsBackground = true };
            server3.Start();

            Thread server4 = new Thread(WaysThread) { IsBackground = true };
            server4.Start();

            Thread server5 = new Thread(TourThread) { IsBackground = true };
            server5.Start();
        }

        protected override void OnStop()
        {
            WriteLog("Service stopped.");
        }

        private static readonly Mutex Mut=new Mutex();
        public void WriteLog(string log)
        {
            Mut.WaitOne();
            StreamWriter logWriter = new StreamWriter("c:\\logs\\osmservice_log.txt", true);
            logWriter.Write(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "    " + log + "\r\n");
            logWriter.Flush();
            logWriter.Close();
            Mut.ReleaseMutex();
        }

        private int _progress = 0;

        public void WriteStats(string log)
        {
            StreamWriter logWriter = new StreamWriter("c:\\logs\\osmservice_stat.txt", true);
            logWriter.Write(log);
            logWriter.Flush();
            logWriter.Close();
        }

        public void CleanStatus()
        {
            _progress = 0;
        }

        public void UpdateStatus(float progress, DateTime estimation)
        {
            _progress = 0;
        }

        public void TaskComplete()
        {
        }

        private void RoutingThread(object data)
        {
            PipeSecurity ps = new PipeSecurity();
            ps.AddAccessRule(new PipeAccessRule("Users", PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("CREATOR OWNER", PipeAccessRights.FullControl, AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("SYSTEM", PipeAccessRights.FullControl, AccessControlType.Allow));
            WriteLog("Routing service started.");
            while (true)
            {
                NamedPipeServerStream pipeServer =
                    new NamedPipeServerStream("routing_pipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.WriteThrough, 1024, 1024, ps);
                pipeServer.WaitForConnection();
                WriteLog("Routing now...");
                DateTime startTime = DateTime.Now;
                StreamString ss = new StreamString(pipeServer);
                double lat1 = double.Parse(ss.ReadString());
                double lon1 = double.Parse(ss.ReadString());
                double lat2 = double.Parse(ss.ReadString());
                double lon2 = double.Parse(ss.ReadString());
                try
                {
                    ss.WriteString(
                        _routing.RouteToJson(_routing.FindRoute(new Point2D(lat1, lon1), new Point2D(lat2, lon2))));
                }
                catch (Exception ex)
                {
                    ss.WriteString("[]");
                    WriteLog(ex.Message);
                }
                pipeServer.Close();
                WriteLog("Routing served in " + ((DateTime.Now - startTime).TotalMilliseconds/1000.0).ToString("0.000") +
                         "s");
            }
        }

        private void KNNThread(object data)
        {
            PipeSecurity ps = new PipeSecurity();
            ps.AddAccessRule(new PipeAccessRule("Users", PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("CREATOR OWNER", PipeAccessRights.FullControl, AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("SYSTEM", PipeAccessRights.FullControl, AccessControlType.Allow));
            WriteLog("Nearest service started.");
            while (true)
            {
                NamedPipeServerStream pipeServer =
                    new NamedPipeServerStream("knn_pipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.WriteThrough, 1024, 1024, ps);
                pipeServer.WaitForConnection();
                WriteLog("Nearest...");
                DateTime startTime = DateTime.Now;
                StreamString ss = new StreamString(pipeServer);
                double lat = double.Parse(ss.ReadString());
                double lon = double.Parse(ss.ReadString());
                double distance = double.Parse(ss.ReadString());
                string json = "[";
                foreach (ElementDistance<RoadLeaf> leaf in _routing.GetKNearestRoads(lat, lon, distance))
                {
                    json += "[[" + leaf.Element.StartNode.X + "," + leaf.Element.StartNode.Y + "],[" +
                            leaf.Element.EndNode.X + "," + leaf.Element.EndNode.Y + "]],";
                }
                json = json.TrimEnd(',') + "]";
                ss.WriteString(json);
                pipeServer.Close();
                WriteLog("Nearest served in " + ((DateTime.Now - startTime).TotalMilliseconds/1000.0).ToString("0.000") +
                         "s");
            }
        }

        private void MatchingThread(object data)
        {
            PipeSecurity ps = new PipeSecurity();
            ps.AddAccessRule(new PipeAccessRule("Users", PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("CREATOR OWNER", PipeAccessRights.FullControl, AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("SYSTEM", PipeAccessRights.FullControl, AccessControlType.Allow));
            WriteLog("Matching service started.");
            while (true)
            {
                NamedPipeServerStream pipeServer =
                    new NamedPipeServerStream("match_pipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.WriteThrough, 1024, 1024, ps);
                pipeServer.WaitForConnection();
                WriteLog("Matching...");
                DateTime startTime = DateTime.Now;
                StreamString ss = new StreamString(pipeServer);
                string track = ss.ReadString();
                string type = ss.ReadString();
                string[] points = track.Split(';');
                List<WayPoint> rawData = new List<WayPoint>();
                int id = 0;
                foreach (string point in points)
                {
                    string[] col = point.Split(',');
                    rawData.Add(new WayPoint(DateTime.Parse(col[0]),
                        new Point2D(double.Parse(col[1]), double.Parse(col[2])), float.Parse(col[3]),
                        float.Parse(col[4]), id++));
                }
                WriteStats("\nrawdata");
                Viterbi vi = new Viterbi(this, _routing, rawData);
                if (type == "route")
                    ss.WriteString(vi.GetRoadListJson());
                //ss.WriteString(vi.GetRouteJson());
                else if (type == "fixed")
                    ss.WriteString(vi.GetFixedJson2());
                else if (type == "debug")
                    ss.WriteString(vi.GetDebugJson());
                pipeServer.Close();
                WriteLog("Matching served in " + ((DateTime.Now - startTime).TotalMilliseconds / 1000.0).ToString("0.000") +
                         "s");
            }
        }

        private void WaysThread(object data)
        {
            PipeSecurity ps = new PipeSecurity();
            ps.AddAccessRule(new PipeAccessRule("Users", PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("CREATOR OWNER", PipeAccessRights.FullControl, AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("SYSTEM", PipeAccessRights.FullControl, AccessControlType.Allow));
            WriteLog("Ways service started.");
            while (true)
            {
                NamedPipeServerStream pipeServer =
                    new NamedPipeServerStream("ways_pipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.WriteThrough, 1024, 1024, ps);
                pipeServer.WaitForConnection();
                WriteLog("Ways...");
                DateTime startTime = DateTime.Now;
                StreamString ss = new StreamString(pipeServer);
                double lat1 = double.Parse(ss.ReadString());
                double lon1 = double.Parse(ss.ReadString());
                double lat2 = double.Parse(ss.ReadString());
                double lon2 = double.Parse(ss.ReadString());
                ss.WriteString(_routing.BoxQuery(lat1, lon1, lat2, lon2));
                pipeServer.Close();
                WriteLog("Ways served in " + ((DateTime.Now - startTime).TotalMilliseconds / 1000.0).ToString("0.000") +
                         "s");
            }
        }

        private void TourThread(object data)
        {
            try
            {
                PipeSecurity ps = new PipeSecurity();
                ps.AddAccessRule(new PipeAccessRule("Users",
                    PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                    AccessControlType.Allow));
                ps.AddAccessRule(new PipeAccessRule("CREATOR OWNER", PipeAccessRights.FullControl,
                    AccessControlType.Allow));
                ps.AddAccessRule(new PipeAccessRule("SYSTEM", PipeAccessRights.FullControl, AccessControlType.Allow));
                WriteLog("Tour service started.");
                while (true)
                {
                    NamedPipeServerStream pipeServer =
                        new NamedPipeServerStream("tour_pipe", PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                            PipeOptions.WriteThrough, 1024, 1024, ps);
                    pipeServer.WaitForConnection();
                    WriteLog("Begin touring...");
                    DateTime startTime = DateTime.Now;
                    StreamString ss = new StreamString(pipeServer);
                    string[] roads = ss.ReadString().Split(',');
                    string[] start = ss.ReadString().Split(',');
                    string[] end = ss.ReadString().Split(',');
                    WriteLog(String.Join(",", roads));
                    WriteLog(String.Join(",", start));
                    WriteLog(String.Join(",", end));
                    string time = ss.ReadString();
                    Point2D startPoint = new Point2D(double.Parse(start[0]), double.Parse(start[1]));
                    Point2D endPoint = new Point2D(double.Parse(end[0]), double.Parse(end[1]));
                    TourBuilder tourBuilder = new TourBuilder(roads, startPoint, endPoint, _routing, time);
                    ss.WriteString(tourBuilder.GetTour());
                    pipeServer.Close();
                    WriteLog("Touring served in " +
                             ((DateTime.Now - startTime).TotalMilliseconds/1000.0).ToString("0.000") +
                             "s");
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.ToString());
            }
        }
    }
}
