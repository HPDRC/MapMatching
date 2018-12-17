using System.Collections.Generic;

namespace MapMatchingLib.Tour
{
    public class TourScriptManager
    {
        private List<TourScript> _scripts;

        public TourScriptManager(List<TourScript> scripts)
        {
            _scripts = scripts;
        }

        public override string ToString()
        {
            string scriptStr = "([";
            foreach (TourScript script in _scripts)
            {
                string s = script.ToString();
                if (s!="")
                    scriptStr += script+",";
            }
            return scriptStr.TrimEnd(',') + "])";
        }
    }
}