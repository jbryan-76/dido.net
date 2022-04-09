using System.Diagnostics;

namespace AnywhereNET
{
    internal class RunnerStatusMessage : IMessage
    {
        // TODO: environment info message? #cores, total ram, OS, etc?

        public enum RunnerStatus
        {
            Starting,
            Stopping,
            BeginJob,
            UpdateJob,
            FinishJob
        }

        public RunnerStatus Status { get; set; }

        public float CpuUtilization { get; set; }

        public RunnerStatusMessage() { }

        public RunnerStatusMessage(RunnerStatus status, int numJobs)//, PerformanceCounter cpuCounter)
        {
            Status = status;

        }

        public void Read(Stream stream)
        {
            //int length = stream.ReadInt32BE();
            //Bytes = stream.ReadBytes(length);
        }

        public void Write(Stream stream)
        {
            //stream.WriteInt32BE(Bytes.Length);
            //stream.Write(Bytes);
        }
    }
}