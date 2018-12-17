namespace MapMatchingLib.Tour
{
    public class MovieScript: TourScript
    {
        public string TrackId;
        public double StartSecond;
        public double EndSecond;
        public override string ToString()
        {
            return "{type:'movie',id:'" + TrackId + "',start:" + StartSecond + ",end:" + EndSecond + "}";
        }
    }
}