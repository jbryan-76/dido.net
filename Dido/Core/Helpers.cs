using System;
using System.IO;

namespace DidoNet
{
    /// <summary>
    /// 
    /// </summary>
    internal static class Helpers
    {
        /// <summary>
        /// Selects a suitable remote runner to execute a task based on the provided configuration.
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        /// <exception cref="RunnerNotAvailableException"></exception>
        /// <exception cref="UnhandledMessageException"></exception>
        internal static Uri SelectRunner(Configuration configuration)
        {
            // prefer the explicitly provided runner...
            var runnerUri = configuration.RunnerUri;

            // ...however if no runner is configured but a mediator is, ask the mediator to choose a runner
            if (runnerUri == null)
            {
                if (configuration.MediatorUri == null)
                {
                    throw new InvalidConfigurationException($"Configuration error: At least one of {nameof(Configuration.MediatorUri)} or {nameof(Configuration.RunnerUri)} must be set to a valid value.");
                }
                else
                {
                    var connectionSettings = new ClientConnectionSettings
                    {
                        ValidaionPolicy = configuration.ServerCertificateValidationPolicy,
                        Thumbprint = configuration.ServerCertificateThumbprint
                    };

                    // open a connection to the mediator
                    using (var mediatorConnection =
                        new Connection(configuration.MediatorUri.Host, configuration.MediatorUri.Port, null, connectionSettings))
                    {
                        // create the communications channel and request an available runner from the mediator
                        var applicationChannel = new MessageChannel(mediatorConnection, Constants.MediatorApp_ChannelId);
                        applicationChannel.Send(new RunnerRequestMessage(configuration.RunnerOSPlatforms, configuration.RunnerLabel, configuration.RunnerTags));

                        // receive and process the response
                        var message = applicationChannel.ReceiveMessage(configuration.MediatorTimeout);
                        switch (message)
                        {
                            case RunnerResponseMessage response:
                                runnerUri = new Uri(response.Endpoint);
                                break;

                            case RunnerNotAvailableMessage notAvailable:
                                throw new RunnerNotAvailableException();

                            default:
                                throw new UnhandledMessageException(message);
                        }
                    }

                }
            }

            return runnerUri;
        }

        /// <summary>
        /// Processes messages received on the assemblies channel.
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        /// <exception cref="UnhandledMessageException"></exception>
        internal static async void HandleAssemblyMessages(Configuration configuration, IMessage message, MessageChannel channel)
        {
            switch (message)
            {
                // resolve a requested assembly and send it back
                case AssemblyRequestMessage request:
                    if (string.IsNullOrEmpty(request.AssemblyName))
                    {
                        var response = new AssemblyErrorMessage(new ArgumentNullException(nameof(AssemblyRequestMessage.AssemblyName)));
                        channel.Send(response);
                        return;
                    }

                    try
                    {
                        var stream = await configuration.ResolveLocalAssemblyAsync(request.AssemblyName);
                        if (stream == null)
                        {
                            channel.Send(new AssemblyErrorMessage(
                                new FileNotFoundException($"Assembly '{request.AssemblyName}' could not be resolved."))
                            );
                        }
                        else
                        {
                            using (stream)
                            using (var mem = new MemoryStream())
                            {
                                stream.CopyTo(mem);
                                channel.Send(new AssemblyResponseMessage(mem.ToArray()));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        channel.Send(new AssemblyErrorMessage(ex));
                    }
                    break;
                default:
                    throw new UnhandledMessageException(message);
            }
        }
    }

}
