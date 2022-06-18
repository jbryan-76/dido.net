using System.IO;

namespace DidoNet
{
    public interface IMessage
    {
        void Write(Stream stream);

        void Read(Stream stream);
    }

    // TODO: make an abstract base Message class too with support for a message version?
}