using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;

namespace Unity.ClusterDisplay
{
    /// <summary>
    /// Base class for classes implementing the different states of a <see cref="ClusterNode"/>.
    /// </summary>
    /// <remarks>Some NodeStateV2 might implement <see cref="System.IDisposable"/>, so anybody "owning" a
    /// <see cref="NodeState"/> of any type should think of testing for <see cref="System.IDisposable"/> and calling
    /// the <see cref="System.IDisposable.Dispose"/> method when getting rid of it.</remarks>
    abstract class NodeState
    {
        /// <summary>
        /// Method to be called every time a frame is to be processed by that <see cref="NodeState"/>.
        /// </summary>
        /// <returns>Either: Not null <see cref="NodeState"/> with unset <see cref="DoFrameResult"/> to indicate that
        /// the <see cref="NodeState"/> is over and current frame needs to execute the returned <see cref="NodeState"/>
        /// before completing the frame.<br/><br/>Or: A null <see cref="NodeState"/> and a set
        /// <see cref="DoFrameResult"/> to indicate that this step is done executing the frame (and it must be called
        /// again for next frame).</returns>
        public unsafe (NodeState, DoFrameResult?) DoFrame()
        {
            var metadata = stackalloc ProfilerMarkerData[1];
            metadata[0].Type = (byte)ProfilerMarkerDataType.UInt64;
            metadata[0].Size = (uint)UnsafeUtility.SizeOf<ulong>();
            ulong frameIndex = Node.FrameIndex;
            metadata[0].Ptr = UnsafeUtility.AddressOf(ref frameIndex);
            IntPtr markerHandle = GetProfilerMarker();
            ProfilerUnsafeUtility.BeginSampleWithMetadata(markerHandle, 1, metadata);
            try
            {
                return DoFrameImplementation();
            }
            finally
            {
                ProfilerUnsafeUtility.EndSample(markerHandle);
            }
        }

        /// <summary>
        /// Implementation of <see cref="DoFrame"/> to be defined by specializing classes that is called every time a
        /// frame is to be processed by that <see cref="NodeState"/>.
        /// </summary>
        /// <returns>Either: Not null <see cref="NodeState"/> with unset <see cref="DoFrameResult"/> to indicate that
        /// the <see cref="NodeState"/> is over and current frame needs to execute the returned <see cref="NodeState"/>
        /// before completing the frame.<br/><br/>Or: A null <see cref="NodeState"/> and a set
        /// <see cref="DoFrameResult"/> to indicate that this step is done executing the frame (and it must be called
        /// again for next frame).</returns>
        protected abstract (NodeState, DoFrameResult?) DoFrameImplementation();

        /// <summary>
        /// Method to be implemented by specializing classes to return a static variable fill by calling
        /// <see cref="CreateProfilingMarker"/>.
        /// </summary>
        /// <remarks>I agree we could do this using class attributes, a dictionary, ...  But a virtual method is the
        /// fastest way of doing it.</remarks>
        protected abstract IntPtr GetProfilerMarker();

        /// <summary>
        /// Node we are a state of.
        /// </summary>
        protected ClusterNode Node { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="node">Node we are a state of.</param>
        protected NodeState(ClusterNode node)
        {
            Node = node;
        }

        /// <summary>
        /// Creates a profiling marker (with metadata to store the frame index) to be returned by GetProfilerMarker.
        /// </summary>
        /// <param name="stateName">Name of the specializing class.</param>
        /// <returns>The created profiling marker.</returns>
        /// <remarks>Should be called only once to initialize a static variable of the specializing class.</remarks>
        protected static IntPtr CreateProfilingMarker(string stateName)
        {
            var handle = ProfilerUnsafeUtility.CreateMarker(stateName, ProfilerUnsafeUtility.CategoryScripts,
                MarkerFlags.Default | MarkerFlags.AvailabilityNonDevelopment, 1);
            ProfilerUnsafeUtility.SetMarkerMetadata(handle, 0, "Frame index", (byte)ProfilerMarkerDataType.UInt64,
                (byte)ProfilerMarkerDataUnit.Count);
            return handle;
        }
    }

    /// <summary>
    /// Type-safe variant of <see cref="NodeState"/>
    /// </summary>
    /// <typeparam name="T">The local node type</typeparam>
    abstract class NodeState<T> : NodeState where T : ClusterNode
    {
        protected new T Node => base.Node as T;

        protected NodeState(T node) : base(node)
        {
        }
    }
}
