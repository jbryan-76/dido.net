using System.Text;
using Xunit.Abstractions;

namespace DidoNet.Test.Common
{
    internal class OutputConverter : TextWriter
    {
        ITestOutputHelper _output;

        string? Filename;

        public OutputConverter(ITestOutputHelper output, string? filename = null)
        {
            _output = output;
            Filename = filename;
            if (!string.IsNullOrEmpty(Filename))
            {
                File.Delete(Filename);
            }
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public override void WriteLine(string message)
        {
            _output.WriteLine(message);
            if (!string.IsNullOrEmpty(Filename))
            {
                int tries = 10;
                while (tries-- > 0)
                {
                    lock (Filename)
                    {
                        try
                        {
                            File.AppendAllLines(Filename, new string[] { message });
                            return;
                        }
                        catch (Exception ex)
                        {
                            Thread.Sleep(1);
                        }
                    }
                }
            }
        }

        public override void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(format, args));
        }

        public override void Write(char value)
        {
            throw new NotSupportedException("This text writer only supports WriteLine(string) and WriteLine(string, params object[]).");
        }
    }
}