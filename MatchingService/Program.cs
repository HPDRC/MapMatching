using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using MapMatchingLib.Astar;

namespace MatchingService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
            //Test();
        }

        private static void Test()
        {
            Random rand = new Random(1000);
            int nodeCount = 200;
            int testTime = 50;
            /*
            Graph g = new Graph();
            Node[] nodes = new Node[nodeCount];
            Trace.WriteLine("Building graph...");

            for (int i = 0; i < nodeCount; i++)
            {
                nodes[i] = g.AddNode(i, rand.Next(0, 100000), rand.Next(0, 100000));
            }

            Trace.WriteLine("Adding arcs...");
            for (int i = 0; i < nodeCount; i++)
            {
                for (int j = i + 1; j < nodeCount; j++)
                {
                    g.AddArc(nodes[i], nodes[j], rand.Next(0, 100));
                    g.AddArc(nodes[j], nodes[i], rand.Next(0, 100));
                }
            }
            Stream streamWrite = File.Create("GraphSaved.bin");
            BinaryFormatter binaryWrite = new BinaryFormatter();
            binaryWrite.Serialize(streamWrite, g);
            streamWrite.Close();
            */
            Console.WriteLine("Loading graph...");
            Stream streamRead = File.OpenRead("GraphSaved.bin");
            BinaryFormatter binaryRead = new BinaryFormatter();
            Graph g = (Graph)binaryRead.Deserialize(streamRead);
            streamRead.Close();

            Console.WriteLine("Start benchmark...");
            DateTime start = DateTime.Now;
            for (int i = 0; i < testTime; i++)
            {
                DateTime beginTime = DateTime.Now;
                AStar AS = new AStar(g);
                AS.SearchPath(g.GetNode(0), g.GetNode(100));
                //AS.SearchPath(nodes[rand.Next(0, nodeCount)], nodes[rand.Next(0, nodeCount)]);
                Console.WriteLine("Task" + i + ": " + ((DateTime.Now - beginTime).TotalMilliseconds / 1000.0).ToString("0.000") +
                                " seconds.");
            }
            double totalSec = (DateTime.Now - start).TotalMilliseconds / 1000.0;
            Console.WriteLine(totalSec.ToString("0.000") + " seconds total.");
            Console.WriteLine((totalSec / testTime).ToString("0.000") + " seconds average.");
            //Console.ReadKey();
        }
    }
}
