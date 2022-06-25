using System;

namespace DidoNet
{
    /// <summary>
    /// Configures a mediator.
    /// </summary>
    public class MediatorConfiguration
    {
        /// <summary>
        /// The unique id of the server instance.
        /// If not provided, a random unique id is used.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // TODO: "job mode": generate an optional id for an execution request, monitor the job, "save" the result
        // TODO: delegate for optional data persistence. default is in-memory
    }
}