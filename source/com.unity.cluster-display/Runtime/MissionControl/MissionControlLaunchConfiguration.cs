﻿using System;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.ClusterDisplay.MissionControl.LaunchPad;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Information on the configuration received from MissionControl / LaunchPad.
    /// </summary>
    public class MissionControlLaunchConfiguration
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static MissionControlLaunchConfiguration Instance { get; } = DetectAndCreate();

        /// <summary>
        /// Configuration of the launchpad that launched this process.
        /// </summary>

        // ReSharper disable once MemberCanBePrivate.Global
        public Config LaunchPadConfig { get; }

        /// <summary>
        /// Raw list <see cref="LaunchCatalog.LaunchParameter.Id"/> with their values.
        /// </summary>
        public JObject RawLaunchData { get; }

        /// <summary>
        /// Check if we have been launched from MissionControl's LaunchPad and if so create a
        /// <see cref="MissionControlLaunchConfiguration"/>.
        /// </summary>
        /// <returns></returns>
        static MissionControlLaunchConfiguration DetectAndCreate()
        {
            var envLaunchPadConfig = Environment.GetEnvironmentVariable("LAUNCHPAD_CONFIG");
            if (envLaunchPadConfig == null)
            {
                return null;
            }

            return new(envLaunchPadConfig, Environment.GetEnvironmentVariable("LAUNCHABLE_DATA"),
                Environment.GetEnvironmentVariable("LAUNCH_DATA"));
        }

        /// <summary>
        /// Constructor (extracting the configuration)
        /// </summary>
        /// <param name="launchPadConfig">Serialized launchpad configuration.</param>
        /// <param name="launchableData">Block of information hardcoded in the catalog for the launchable we are
        /// running.</param>
        /// <param name="launchData">All launch parameters (global, launch complex and launch pad).</param>

        // ReSharper disable once UnusedParameter.Local
        MissionControlLaunchConfiguration(string launchPadConfig, string launchableData, string launchData)
        {
            LaunchPadConfig = JsonConvert.DeserializeObject<Config>(launchPadConfig, Json.SerializerOptions);
            RawLaunchData = string.IsNullOrEmpty(launchData) ? new() : JObject.Parse(launchData);
        }

        /// <summary>
        /// Compute the node id for this node.
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no NodeId was specified as part of the LaunchData.  This
        /// shouldn't normally happen as Capcom should have computed a NodeId for every node.</remarks>
        public byte? GetNodeId()
        {
            return (byte?)RawLaunchData.Value<int?>(LaunchParameterConstants.NodeIdParameterId);
        }

        /// <summary>
        /// Compute the <see cref="NodeRole"/> for this node.
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no NodeRole was specified as part of the LaunchData.
        /// This shouldn't normally happen as Capcom should have computed a NodeRole for every node.</remarks>
        public NodeRole? GetNodeRole()
        {
            var nodeRole = RawLaunchData.Value<string>(LaunchParameterConstants.NodeRoleParameterId);
            return nodeRole switch
            {
                LaunchParameterConstants.NodeRoleEmitter => NodeRole.Emitter,
                LaunchParameterConstants.NodeRoleRepeater => NodeRole.Repeater,
                LaunchParameterConstants.NodeRoleBackup => NodeRole.Backup,
                _ => null
            };
        }

        /// <summary>
        /// Gets the number of repeater nodes.
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no RepeaterCount was specified as part of the LaunchData.
        /// This shouldn't normally happen as Capcom should have computed a RepeaterCount for all nodes of the cluster.
        /// </remarks>
        public int? GetRepeaterCount()
        {
            return RawLaunchData.Value<int?>(LaunchParameterConstants.RepeaterCountParameterId);
        }

        /// <summary>
        /// Gets the number of backup nodes.
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no BackupCount was specified as part of the LaunchData.
        /// This shouldn't normally happen as Capcom should have computed an effective BackupCount for all nodes of the
        /// cluster.</remarks>
        public int? GetBackupCount()
        {
            return RawLaunchData.Value<int?>(LaunchParameterConstants.BackupNodeCountParameterId);
        }

        /// <summary>
        /// Gets the IPv4 multicast UDP address used for inter-node communication (state propagation).
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no MulticastAddress was specified as part of the
        /// LaunchData.</remarks>
        [CanBeNull]
        public string GetMulticastAddress()
        {
            return RawLaunchData.Value<string>(LaunchParameterConstants.MulticastAddressParameterId);
        }

        /// <summary>
        /// Gets the multicast UDP port used for inter-node communication (state propagation).
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no MulticastPort was specified as part of the LaunchData.
        /// </remarks>
        public int? GetMulticastPort()
        {
            return RawLaunchData.Value<int?>(LaunchParameterConstants.MulticastPortParameterId);
        }

        /// <summary>
        /// Gets the Network adapter name (or ip) identifying the network adapter to use for inter-node communication
        /// (state propagation).
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no MulticastAdapterName was specified as part of neither
        /// the LaunchData or LaunchPadConfig.</remarks>
        [CanBeNull]
        public string GetMulticastAdapterName()
        {
            return RawLaunchData.Value<string>(LaunchParameterConstants.MulticastAdapterNameParameterId) ??
                LaunchPadConfig.ClusterNetworkNic;
        }

        /// <summary>
        /// Gets the multicast UDP port used for inter-node communication (state propagation).
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no TargetFrameRate was specified as part of the
        /// LaunchData.</remarks>
        public int? GetTargetFrameRate()
        {
            var targetFrameRate = RawLaunchData.Value<int?>(LaunchParameterConstants.TargetFrameRateParameterId);
            if (!targetFrameRate.HasValue)
            {
                return null;
            }

            // We may be setting this result to something like Application.targetFrameRate down stream, therefore set it
            // to -1 so the FPS is unlimited if we get an invalid result:
            // https://docs.unity3d.com/ScriptReference/Application-targetFrameRate.html
            if (targetFrameRate.Value <= 0)
            {
                return -1;
            }

            return targetFrameRate;
        }

        /// <summary>
        /// Gets if we should delay repeaters by one frame.
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no DelayRepeaters was specified as part of the
        /// LaunchData.</remarks>
        public bool? GetDelayRepeaters()
        {
            return RawLaunchData.Value<bool?>(LaunchParameterConstants.DelayRepeatersParameterId);
        }

        /// <summary>
        /// Disables rendering of the emitter (headless).
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no HeadlessEmitter was specified as part of the
        /// LaunchData.</remarks>
        public bool? GetHeadlessEmitter()
        {
            return RawLaunchData.Value<bool?>(LaunchParameterConstants.HeadlessEmitterParameterId);
        }

        /// <summary>
        /// Will shift NodeId used for rendering of repeater nodes (RenderNodeId = NodeId - 1) when used with a headless
        /// emitter.
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no ReplaceHeadlessEmitter was specified as part of the
        /// LaunchData.</remarks>
        public bool? GetReplaceHeadlessEmitter()
        {
            return RawLaunchData.Value<bool?>(LaunchParameterConstants.ReplaceHeadlessEmitterParameterId);
        }

        /// <summary>
        /// Gets timeout for a starting node to perform handshake with the other nodes during cluster startup.
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no HandshakeTimeout was specified as part of the
        /// LaunchData.</remarks>
        public TimeSpan? GetHandshakeTimeout()
        {
            var timeoutSec = RawLaunchData.Value<float?>(LaunchParameterConstants.HandshakeTimeoutParameterId);
            return timeoutSec.HasValue ? TimeSpan.FromSeconds(timeoutSec.Value) : null;
        }

        /// <summary>
        /// Gets timeout for communication once the cluster is started.
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no CommunicationTimeout was specified as part of the
        /// LaunchData.</remarks>
        public TimeSpan? GetCommunicationTimeout()
        {
            var timeoutSec = RawLaunchData.Value<float?>(LaunchParameterConstants.CommunicationTimeoutParameterId);
            return timeoutSec.HasValue ? TimeSpan.FromSeconds(timeoutSec.Value) : null;
        }

        /// <summary>
        /// Gets if the cluster tries to use hardware synchronization?
        /// </summary>
        /// <remarks>A <c>null</c> return value indicates that no EnableHardwareSync was specified as part of the
        /// LaunchData.</remarks>
        public bool? GetEnableHardwareSync()
        {
            return RawLaunchData.Value<bool?>(LaunchParameterConstants.EnableHardwareSyncParameterId);
        }

        /// <summary>
        /// Returns the port MissionControl's capsule should listen on for new capcom connections.
        /// </summary>
        public int GetCapsulePort()
        {
            return RawLaunchData.Value<int?>(LaunchParameterConstants.CapsuleBasePortParameterId) ??
                LaunchParameterConstants.DefaultCapsuleBasePort;
        }
    }
}
