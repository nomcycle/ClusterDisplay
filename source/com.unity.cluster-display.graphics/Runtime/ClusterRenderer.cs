﻿using System;
using UnityEngine;
using Unity.ClusterDisplay.Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Manages Cluster Display rendering.
    /// </summary>
    /// <remarks>
    /// Instantiate this component to render using cluster-specific projections.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Make sure ClusterRenderer executes late.
    public class ClusterRenderer : SingletonMonoBehaviour<ClusterRenderer>
    {
        internal static bool IsActive()
        {
            // ReSharper disable once RedundantArgumentDefaultValue -> I know logError is false by default but we really don't want error messages in the event someone would decide to change the default value...
            if (TryGetInstance(out var instance, false))
            {
                return instance.isActiveAndEnabled;
            }

            return false;
        }

        internal static Action Enabled = delegate { };
        internal static Action Disabled = delegate { };

        /// <summary>
        /// Placeholder type introduced since the PlayerLoop API requires types to be provided for the injected subsystem.
        /// </summary>
        struct ClusterDisplayUpdate { }

        [SerializeField]
        ClusterRendererSettings m_Settings = new ClusterRendererSettings();

        [SerializeField]
        ProjectionPolicy m_ProjectionPolicy;

        [SerializeField]
        bool m_DelayPresentByOneFrame;

#if CLUSTER_DISPLAY_HDRP
        IPresenter m_Presenter = new HdrpPresenter();
#elif CLUSTER_DISPLAY_URP
        IPresenter m_Presenter = new UrpPresenter();
#else // TODO Add support for Legacy render pipeline.
        IPresenter m_Presenter = new NullPresenter();
#endif

        internal const int VirtualObjectLayer = 12;

        // TODO: Create a custom icon.
        const string k_IconName = "BuildSettings.Metro On@2x";

        internal ProjectionPolicy ProjectionPolicy
        {
            get => m_ProjectionPolicy;
            set => m_ProjectionPolicy = value;
        }

        /// <summary>
        /// If true, buffers and delays the presentation of the camera output by one frame.
        /// </summary>
        internal bool DelayPresentByOneFrame
        {
            get => m_DelayPresentByOneFrame;
            set
            {
                m_DelayPresentByOneFrame = value;
                m_Presenter.SetDelayed(m_DelayPresentByOneFrame);
            }
        }

        /// <summary>
        /// Specifies whether the current projection policy is in debug mode.
        /// </summary>
        internal bool IsDebug
        {
            set
            {
                if (m_ProjectionPolicy != null)
                {
                    m_ProjectionPolicy.IsDebug = value;
                }
            }
            get => m_ProjectionPolicy != null && m_ProjectionPolicy.IsDebug;
        }

        /// <summary>
        /// Gets the current cluster rendering settings.
        /// </summary>
        public ClusterRendererSettings Settings
        {
            get => m_Settings;
            set => m_Settings = value;
        }

        /// <summary>
        /// Gets the camera internally used to present on screen.
        /// </summary>
        public Camera PresentCamera => m_Presenter.Camera;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.DrawIcon(transform.position, k_IconName);
        }
#endif

        // TODO Why do we need to "delete" the base implementation?
        protected override void OnAwake() { }

        void OnValidate()
        {
            m_Presenter.SetDelayed(m_DelayPresentByOneFrame);
        }

        void Reset()
        {
            foreach (var projectionPolicy in GetComponents<ProjectionPolicy>())
            {
                if (projectionPolicy != m_ProjectionPolicy)
                {
                    DestroyImmediate(projectionPolicy);
                }
            }
        }

        void OnEnable()
        {
            if (Application.isPlaying && ClusterRenderingSettings.Current.PersistOnSceneChange)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (Application.isPlaying && m_ProjectionPolicy != null)
            {
                ClusterDebug.Log($"Cluster Rendering is enabled with {m_ProjectionPolicy.GetType()}." +
                    $" The current screen resolution is {Screen.width} x {Screen.height}.");
            }

            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync) &&
                clusterSync.NodeRole is NodeRole.Backup && clusterSync.RepeatersDelayedOneFrame)
            {
                clusterSync.OnNodeRoleChanged += NodeRoleChanged;
            }

            m_Presenter.SetDelayed(m_DelayPresentByOneFrame);
            m_Presenter.Enable(gameObject);
            m_Presenter.Present += OnPresent;

            PlayerLoopExtensions.RegisterUpdate<UnityEngine.PlayerLoop.PostLateUpdate, ClusterDisplayUpdate>(OnClusterDisplayUpdate);

#if UNITY_EDITOR
            SceneView.RepaintAll();
#else
            IsDebug = false;
#endif
            Enabled.Invoke();
        }

        void NodeRoleChanged()
        {
            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync) &&
                clusterSync.NodeRole is NodeRole.Emitter && clusterSync.RepeatersDelayedOneFrame)
            {
                DelayPresentByOneFrame = true;
                clusterSync.OnNodeRoleChanged -= NodeRoleChanged;
            }
            else if (clusterSync is {NodeRole: not (NodeRole.Backup or NodeRole.Unassigned)})
            {
                // The node switched to something else than an emitter.  So we will never need to change
                // DelayPresentByOneFrame, so remove this delegate.
                clusterSync.OnNodeRoleChanged -= NodeRoleChanged;
            }
        }

        void Update()
        {
            if (m_ProjectionPolicy != null)
            {
                m_ProjectionPolicy.Origin = transform.localToWorldMatrix;
            }
        }

        void OnDisable()
        {
            Disabled.Invoke();

            PlayerLoopExtensions.DeregisterUpdate<ClusterDisplayUpdate>(OnClusterDisplayUpdate);

            m_Presenter.Present -= OnPresent;
            m_Presenter.Disable();

            if (ServiceLocator.TryGet(out IClusterSyncState clusterSync))
            {
                clusterSync.OnNodeRoleChanged -= NodeRoleChanged;
            }
        }

        void OnPresent(PresentArgs args)
        {
            if (m_ProjectionPolicy != null && m_ProjectionPolicy.enabled)
            {
                m_ProjectionPolicy.Present(args);
            }
        }

        static void OnClusterDisplayUpdate()
        {
            // It may be possible that a subsystem update occurs after de-registration with the new update-loop being used next frame.
            if (TryGetInstance(out var clusterRenderer) && clusterRenderer.isActiveAndEnabled &&
                ClusterCameraManager.Instance.ActiveCamera is { } activeCamera &&
                clusterRenderer.m_ProjectionPolicy is { } projectionPolicy)
            {
                projectionPolicy.UpdateCluster(clusterRenderer.m_Settings, activeCamera);
            }
        }
    }
}
