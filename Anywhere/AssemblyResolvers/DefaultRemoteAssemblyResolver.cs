namespace DidoNet
{
    public class DefaultRemoteAssemblyResolver
    {
        private MessageChannel Channel { get; set; }

        public DefaultRemoteAssemblyResolver(MessageChannel channel)
        {
            Channel = channel;

            // TODO: add a handler and refactor AssemblyResponseMessage
        }

        public Task<Stream?> ResolveAssembly(Environment env, string assemblyName)
        {
            // request the assembly from the application
            var request = new AssemblyRequestMessage(assemblyName);
            Channel.Send(request);

            // receive the response
            var response = Channel.ReceiveMessage<AssemblyResponseMessage>();

            // the current caller will dispose the stream, so wrap the response
            // in another stream to keep the channel open
            // TODO: use a different pattern. byte[] all the way?
            return response.ContentType == AssemblyResponseMessage.ContentTypes.Error
                ? Task.FromResult<Stream?>(null)
                : Task.FromResult<Stream?>(new MemoryStream(response.Bytes));
        }
    }
}