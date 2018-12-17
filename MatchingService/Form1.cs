using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Threading;
using System.Windows.Forms;
using MapMatchingLib.Astar;
using MapMatchingLib.mvideo;
using MapMatchingLib.MapMatching;
using MapMatchingLib.MultiCore.Generic;
using MapMatchingLib.Routing;
using MapMatchingLib.RTree;
using MapMatchingLib.SysTools;

namespace MatchingService
{
    public partial class Form1 : Form, ILogger,IProgressReporter
    {

        private RoutingMachine _routing;
        private readonly GraphLoader _loader;

        public Form1()
        {
            InitializeComponent();
            //_logWriter = new StreamWriter("c:\\osm_log.txt", true);
            _loader = new GraphLoader(this, this, GraphLoaded, "miami");
            _loader.LoadGraph();
        }

        #region Abstract overrides

        //private readonly StreamWriter _logWriter;

        private delegate void UpdateStatusCallback(float progress, DateTime estimation);

        public void UpdateStatus(float progress, DateTime estimation)
        {
            if (statusStrip1.InvokeRequired)
            {
                UpdateStatusCallback d = UpdateStatus;
                Invoke(d, progress, estimation);
            }
            else
            {
                if (progress < 0) progress = 0;
                if (progress > 1) progress = 1;
                lblFinishTime.Text = @"Finish Time:" + estimation.ToString("MM-dd HH:mm:ss");
                lblProgress.Text = @"Progress:" + (100*progress).ToString("0.00") + @"%";
                progressBar.Value = Math.Min((int) (100*progress), 100);
            }
        }

        private delegate void CleanStatusCallback();

        public void CleanStatus()
        {
            if (statusStrip1.InvokeRequired)
            {
                CleanStatusCallback d = CleanStatus;
                Invoke(d);
            }
            else
            {
                lblFinishTime.Text = "";
                lblProgress.Text = "";
                progressBar.Value = 0;
            }
        }

        private delegate void WriteLogCallback(string log);

        public void WriteLog(string log)
        {
            if (txtLog.InvokeRequired)
            {
                WriteLogCallback d = WriteLog;
                Invoke(d, log);
            }
            else
            {
                lock (txtLog)
                {
                    if (log != "")
                        txtLog.AppendText(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "    " + log + "\r\n");
                    else
                        txtLog.AppendText("*****************************************************\r\n");
                    txtLog.Refresh();
                }
            }
        }

        public void WriteStats(string log)
        {
            if (txtLog.InvokeRequired)
            {
                WriteLogCallback d = WriteStats;
                Invoke(d, log);
            }
            else
            {
                StreamWriter logWriter = new StreamWriter("c:\\osm_log.txt", true);
                logWriter.Write(log);
                logWriter.Flush();
                logWriter.Close();
            }
        }

        private delegate void TaskCompleteCallback();

        public void TaskComplete()
        {
            if (InvokeRequired)
            {
                TaskCompleteCallback d = TaskComplete;
                Invoke(d);
            }
            else
            {
                btnTest.Enabled = true;
            }
            btnTest.Enabled = true;
        }

        private delegate double GetParamCallback();
        private delegate void SetParamCallback(double param);
        public double GetSigma()
        {
            if (InvokeRequired)
            {
                GetParamCallback d = GetSigma;
                Invoke(d);
            }
            else
            {
                return double.Parse(txtSigma.Text);
            }
            return double.Parse(txtSigma.Text);
        }
        public double GetBeta()
        {
            if (InvokeRequired)
            {
                GetParamCallback d = GetBeta;
                Invoke(d);
            }
            else
            {
                return double.Parse(txtBeta.Text);
            }
            return double.Parse(txtBeta.Text);
        }
        public void SetSigma(double sigma)
        {
            if (InvokeRequired)
            {
                SetParamCallback d = SetSigma;
                Invoke(d, sigma);
            }
            else
            {
                txtSigma.Text = sigma.ToString("0.0000", CultureInfo.InvariantCulture);
            }
            txtSigma.Text = sigma.ToString("0.0000", CultureInfo.InvariantCulture);
        }
        public void SetBeta(double beta)
        {
            if (InvokeRequired)
            {
                SetParamCallback d = SetBeta;
                Invoke(d, beta);
            }
            else
            {
                txtBeta.Text = beta.ToString("0.0000", CultureInfo.InvariantCulture);
            }
            txtBeta.Text = beta.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        public double GetDiffPercent()
        {
            if (InvokeRequired)
            {
                GetParamCallback d = GetDiffPercent;
                Invoke(d);
            }
            else
            {
                return double.Parse(txtPercent.Text);
            }
            return double.Parse(txtPercent.Text);
        }

        public double GetDistance()
        {
            if (InvokeRequired)
            {
                GetParamCallback d = GetDistance;
                Invoke(d);
            }
            else
            {
                return double.Parse(txtDistance.Text);
            }
            return double.Parse(txtDistance.Text);
        }
        public double GetInterval()
        {
            if (InvokeRequired)
            {
                GetParamCallback d = GetInterval;
                Invoke(d);
            }
            else
            {
                return double.Parse(txtInterval.Text);
            }
            return double.Parse(txtInterval.Text);
        }

        #endregion Abstract overrides

        //private void btnLoadGraph_Click(object sender, EventArgs e)
        //{
        //    loader = new GraphLoader(this, GraphLoaded, "miami");
        //    loader.LoadGraph();
        //}

        //Dictionary<string, int> dictWayType = new Dictionary<string, int> {{"motorway",0},{"motorway_link",1},{"trunk",2},{"trunk_link",3},
        //        {"primary",4},{"primary_link",5},{"secondary",6},{"secondary_link",7},
        //        {"tertiary",8},{"tertiary_link",9},{"unclassified",10},{"service",11},
        //        {"residential",12},{"track",13},{"path",14}};




        private void GraphLoaded()
        {
            //double lat1 = 25.761247361080606;
            //double lon1 = -80.3605270385742;
            //double lat2 = 25.75027022126175;
            //double lon2 = -80.37983894348145;
            //double lat1 = 25.70944491714431;
            //double lon1 = -80.42781829833984;
            //double lat2 = 25.749342529149743;
            //double lon2 = -80.33323287963866;

            //WriteLog("Starting search...");
            //DateTime start = DateTime.Now;
            //for (int i = 0; i < 10; i++)
            //{
            //    _routing.FindRoute(lat1, lon1, lat2, lon2);
            //}
            //WriteLog("done:" + ((DateTime.Now - start).TotalMilliseconds / 1000).ToString("0.000"));
            _routing = _loader.RMachine;
            Thread server1 = new Thread(RoutingThread) {IsBackground = true};
            server1.Start();

            Thread server2 = new Thread(KNNThread) {IsBackground = true};
            server2.Start();

            Thread server3 = new Thread(MatchingThread) { IsBackground = true };
            server3.Start();

            Thread server4 = new Thread(WaysThread) { IsBackground = true };
            server4.Start();
            
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
                foreach (string point in points)
                {
                    string[] col = point.Split(',');
                    rawData.Add(new WayPoint(DateTime.Parse(col[0]),
                        new Point2D(double.Parse(col[1]), double.Parse(col[2])), float.Parse(col[3]),
                        float.Parse(col[4])));
                }
                WriteStats("\nrawdata");
                double sigma = GetSigma();
                double beta = GetBeta();
                double distance = GetDistance();
                double interval = GetInterval();
                double percent = GetDiffPercent();
                Viterbi vi = new Viterbi(this, _routing, rawData, sigma, beta, distance, interval, percent);
                if (type == "route")
                    ss.WriteString(vi.GetRouteJson());
                else if (type == "fixed")
                    ss.WriteString(vi.GetFixedJson());
                else if (type == "debug")
                    ss.WriteString(vi.GetDebugJson());
                pipeServer.Close();
                WriteLog("Matching served in " + ((DateTime.Now - startTime).TotalMilliseconds/1000.0).ToString("0.000") +
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

        private void btnTest_Click(object sender, EventArgs e)
        {
            SqlHelper s = new SqlHelper();
            SqlDataReader reader = s.GetReader("SELECT [id],[route] FROM [mvideo].[dbo].[routes] WHERE [lat_max]<25.8259 AND [lat_min]>25.6641 AND [lon_max]<-80.1648 AND [lon_min]>-80.4292");
            int i = 0;
            WriteStats("id\traw_count\tfiltered_count\tavg_speed\tfiltered\ttime\traw_count\tfiltered_count\tavg_speed\tfiltered\ttime");
            while (reader.Read())
            {
                if (File.Exists(@"c:\matching_images\" + reader["id"] + ".png"))continue;
                WriteLog("--- Route:" + reader["id"]+" ---");
                WriteStats("\n"+reader["id"]);
                try
                {
                    List<WayPoint> rawData = RouteParser.ParseRoute(reader["route"].ToString());
                    Viterbi vi = new Viterbi(this, _routing, rawData);
                    Bitmap img = vi.GetRouteImage();
                    img.Save(@"c:\matching_images\" + reader["id"] + ".png", ImageFormat.Png);
                    img = vi.GetRouteImage(true);
                    img.Save(@"c:\matching_images\" + reader["id"] + "_compare.png", ImageFormat.Png);
                    i++;
                    //if (i > 5) break;
                }
                catch (Exception ex)
                {
                    WriteStats("error:"+ex);
                }
            }
            s.Close();
            WriteLog("Done.");
        }
    }
}
