namespace AnywhereNET
{
    public interface IMessage
    {
        void Write(Stream stream);
        void Read(Stream stream);
    }
}