namespace DidoNet
{
    /// <summary>
    /// Configures a mediator.
    /// </summary>
    public class MediatorConfiguration
    {
        /// <summary>
        /// The unique id of the server instance.
        /// If not provided, a random unique id will be assigned for each Mediator server when it starts.
        /// </summary>
        public string? Id { get; set; } = string.Empty;

        // TODO: "job mode": generate an optional id for an execution request, monitor the job, "save" the result
        // TODO: delegate for optional data persistence. default is in-memory
    }
}