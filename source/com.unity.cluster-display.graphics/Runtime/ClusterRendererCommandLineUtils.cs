﻿using System;
using System.Runtime.CompilerServices;
using Unity.ClusterDisplay.MissionControl;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// A utility whose purpose is to configure the Cluster Renderer based on command line parameters.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ClusterRenderer))]
    class ClusterRendererCommandLineUtils : MonoBehaviour
    {
        // This needs to execute after the main manager has initialized and before the renderer does.
        // We can manage with default execution order.
        // Ultimately architecture simplification will make this more straightforward / less fragile.
        void OnEnable()
        {
            var renderer = GetComponent<ClusterRenderer>();
            Assert.IsNotNull(renderer);

            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync) &&
                clusterSync.NodeRole is NodeRole.Emitter &&
                clusterSync.EmitterIsHeadless)
            {
                renderer.enabled = false;
                return;
            }

            renderer.DelayPresentByOneFrame = clusterSync.NodeRole is NodeRole.Emitter && clusterSync.RepeatersDelayedOneFrame;
            renderer.Settings.RenderTestPattern = CommandLineParser.testPattern.Defined;

            if (Application.isPlaying && renderer.ProjectionPolicy is { } projectionPolicy)
            {
                ApplyCmdLineSettings(projectionPolicy);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void TrySet<T>(ref T dest, T? maybeValue) where T : struct
        {
            dest = maybeValue ?? dest;
        }

        static void ApplyCmdLineSettings(ProjectionPolicy projection)
        {
            switch (projection)
            {
                case TiledProjection tiledProjection:
                    var settings = tiledProjection.Settings;
                    ParseSettings(ref settings);
                    tiledProjection.Settings = settings;
                    break;
            }
        }

        static void ParseSettings(ref TiledProjectionSettings baseSettings)
        {
            if (CommandLineParser.bezel.Defined)
                TrySet(ref baseSettings.Bezel, CommandLineParser.bezel.Value);

            if (CommandLineParser.gridSize.Defined)
                TrySet(ref baseSettings.GridSize, CommandLineParser.gridSize.Value);

            if (CommandLineParser.physicalScreenSize.Defined)
                TrySet(ref baseSettings.PhysicalScreenSize, CommandLineParser.physicalScreenSize.Value);
        }
    }
}
