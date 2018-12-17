using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapMatchingLib.MapMatching
{
    public class OverlapParts
    {
        public string Id;
        public double StartSecond;
        public double EndSecond;
        public string StartPercent;
        public string EndPercent;
        
        public OverlapParts(string id)
        {
            Id = id;
        }
        public static OverlapParts Parse(string str)
        {
            try
            {
                string[] parts = str.Split(',');
                OverlapParts o = new OverlapParts(parts[0])
                {
                    StartPercent = parts[1],
                    EndPercent = parts[2],
                    StartSecond = double.Parse(parts[3]),
                    EndSecond = double.Parse(parts[4])
                };
                //Id = parts[0];
                return o;
            }
            catch (Exception)
            {
                throw new Exception(str);
            }
        }

        public override string ToString()
        {
            string s = Id + ",";
            s += StartPercent + ",";
            s += EndPercent + ",";
            s += StartSecond.ToString("0.0") + ",";
            s += EndSecond.ToString("0.0") + ";";
            return s;
        }
    }
}
