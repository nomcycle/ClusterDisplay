﻿using System;
using UnityEngine;

namespace Unity.ClusterDisplay.Graphics
{
    static class ApplicationUtil
    {
        public static bool CommandLineArgExists(string arg)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i != args.Length; ++i)
            {
                if (args[i] == arg)
                    return true;
            }

            return false;
        }

        public static bool TryReadCommandLineArg(string arg, out string output)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i != args.Length; ++i)
            {
                if (args[i] == arg)
                {
                    output = args[i + 1];
                    return true;
                }
            }

            output = String.Empty;
            return false;
        }

        public static bool ParseCommandLineArgs(string name, out Vector2Int output)
        {
            output = Vector2Int.zero;
            var str = String.Empty;
            if (TryReadCommandLineArg(name, out str))
            {
                var dim = str.Split('x');
                if (dim.Length != 2)
                {
                    Debug.LogError($"Failed to parse [{name}], expected format like 512x512, got [{str}].");
                    return false;
                }

                int w, h;
                if (!int.TryParse(dim[0], out w))
                {
                    Debug.LogError($"Failed to parse [{name}], unexpected width: [{dim[0]}].");
                    return false;
                }

                if (!int.TryParse(dim[1], out h))
                {
                    Debug.LogError($"Failed to parse [{name}], unexpected height: [{dim[1]}].");
                    return false;
                }

                output = new Vector2Int(w, h);
                return true;
            }

            return false;
        }
        
        public static bool ParseCommandLineArgs(string name, out Vector2 output)
        {
            output = Vector2.zero;
            var str = String.Empty;
            if (TryReadCommandLineArg(name, out str))
            {
                var dim = str.Split('x');
                if (dim.Length != 2)
                {
                    Debug.LogError($"Failed to parse [{name}], expected format like 12.4x54.06, got [{str}].");
                    return false;
                }

                float w, h;
                if (!float.TryParse(dim[0], out w))
                {
                    Debug.LogError($"Failed to parse [{name}], unexpected width: [{dim[0]}].");
                    return false;
                }

                if (!float.TryParse(dim[1], out h))
                {
                    Debug.LogError($"Failed to parse [{name}], unexpected height: [{dim[1]}].");
                    return false;
                }

                output = new Vector2(w, h);
                return true;
            }

            return false;
        }

        public static bool ParseCommandLineArgs(string name, out int output)
        {
            output = 0;
            var str = String.Empty;
            if (TryReadCommandLineArg(name, out str))
            {
                if (int.TryParse(str, out output))
                    return true;
            }
            return false;
        }
    }
}