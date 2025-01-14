using System;

namespace Unity.ClusterDisplay.MissionControl.LaunchPad
{
    /// <summary>
    /// <see cref="Command"/> indicating to the LaunchPad that it should aborts whatever it was doing (so that its
    /// status returns to idle).
    /// </summary>
    public class AbortCommand: Command, IEquatable<AbortCommand>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public AbortCommand()
        {
            Type = CommandType.Abort;
        }

        /// <summary>
        /// Resulting state of the abort command will be over instead of idle.
        /// </summary>
        /// <remarks>Useful to get in the same state as if it would be the payload that exited by itself.</remarks>
        public bool AbortToOver { get; set; }

        public bool Equals(AbortCommand? other)
        {
            return other != null &&
                other.GetType() == typeof(AbortCommand) &&
                other.AbortToOver == AbortToOver;
        }
    }
}
