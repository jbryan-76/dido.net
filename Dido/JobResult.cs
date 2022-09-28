using System;

namespace DidoNet
{
    public class JobResult<Tprop>
    {
        public JobStatus Status { get; set; }
        public Tprop Result { get; set; }
        public Exception? Exception { get; set; }
    }
}
