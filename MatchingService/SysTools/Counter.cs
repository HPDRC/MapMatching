namespace MatchingService.SysTools
{
    public class Counter
    {
        public long Value { get; set; }

        public Counter()
        {
            Value = 0;
        }

        public void Increase()
        {
            Value++;
        }

        public void Increase(int num)
        {
            Value += num;
        }

        public void Increase(long num)
        {
            Value += num;
        }

        public void Decrease()
        {
            Value--;
        }

        public void Decrease(int num)
        {
            Value -= num;
        }

        public void Decrease(long num)
        {
            Value -= num;
        }
    }
}
