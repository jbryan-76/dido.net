using System.IO;

namespace DidoNet
{
    public interface IMessage
    {
        void Write(Stream stream);

        void Read(Stream stream);
    }
}