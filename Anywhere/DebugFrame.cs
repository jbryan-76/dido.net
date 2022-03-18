using System.Text;

namespace AnywhereNET
{
    public class DebugFrame : Frame
    {
        public DebugFrame(string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            FrameType = FrameTypes.Debug;
            Length = bytes.Length;
            Payload = bytes;
        }

        public DebugFrame(Frame frame) : base(frame) { }

        public string Message { get { return Encoding.UTF8.GetString(Payload); } }
    }
}