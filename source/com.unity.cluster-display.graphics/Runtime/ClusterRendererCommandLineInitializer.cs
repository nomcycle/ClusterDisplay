﻿using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    // TODO Is that component too shallow? Should we promote this to the CLusterRenderer?
    [RequireComponent(typeof(ClusterRenderer))]
    class ClusterRendererCommandLineInitializer : MonoBehaviour
    {
        static class CommandLineArgs
        {
            public const string k_Debug = "--debug"; // very common name, collision risk
            public const string k_GridSize = "--gridsize";
            public const string k_Overscan = "--overscan";
            public const string k_Bezel = "--bezel";
            public const string k_PhysicalScreenSize = "--physicalscreensize";
        }

        public void OnEnable()
        {
            var clusterRenderer = GetComponent<ClusterRenderer>();

            if (ApplicationUtil.CommandLineArgExists(CommandLineArgs.k_Debug))
            {
                ((IClusterRenderer)clusterRenderer).Context.Debug = true;
            }

            ParseSettings(clusterRenderer.Settings);
        }

        static void ParseSettings(ClusterRendererSettings settings)
        {
            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_GridSize, out Vector2Int gridSize))
            {
                settings.GridSize = gridSize;
            }

            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_Bezel, out Vector2 bezel))
            {
                settings.Bezel = bezel;
            }

            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_PhysicalScreenSize, out Vector2 physicalScreenSize))
            {
                settings.PhysicalScreenSize = physicalScreenSize;
            }

            if (ApplicationUtil.ParseCommandLineArgs(CommandLineArgs.k_Overscan, out int overscanInPixels))
            {
                settings.OverScanInPixels = overscanInPixels;
            }
        }
    }
}
