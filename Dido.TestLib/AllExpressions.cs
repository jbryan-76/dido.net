namespace DidoNet.TestLib
{
    public class AllExpressions
    {
        public static string ComprehensiveExpressionMethod(string arg0, string arg1, string arg2)
        {
            string result = string.Empty;
            //double result = 0;

            var array = new string[] { "one", "two", "three" };
            result = result + string.Join("", array[0], array[1]);

            if (!string.IsNullOrEmpty(result))
            {
                result += "foo";
            }

            //var now = DateTime.Now;
            //var binaryResult = (now - now).TotalSeconds;

            //result += binaryResult;

            return result;
        }
    }
}