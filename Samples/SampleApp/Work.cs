static class Work
{
    public class Result
    {
        public double Duration { get; set; }
        public long Average { get; set; }
    }

    public static Result DoSomethingLongAndExpensive(int sizeInMB)
    {
        var start = DateTime.Now;

        // allocate a big array and fill it with random numbers
        var rand = new Random();
        var numbers = new int[1024 * 1024 * sizeInMB];
        for (int i = 0; i < numbers.Length; i++)
        {
            numbers[i] = rand.Next();
        }

        // sort it
        Array.Sort(numbers);

        // compute the average value
        long average = 0;
        for (int i = 0; i < numbers.Length; i++)
        {
            average += numbers[i];
        }
        average /= numbers.LongLength;

        return new Result
        {
            Duration = (DateTime.Now - start).TotalSeconds,
            Average = average
        };
    }
}
