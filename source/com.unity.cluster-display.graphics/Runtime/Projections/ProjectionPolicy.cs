using System;
using System.Collections.Generic;
using Unity.ClusterDisplay.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.ClusterDisplay.Graphics
{
    /// <summary>
    /// Performs custom cluster rendering logic.
    /// </summary>
    /// <remarks>
    /// Implement this abstract class to perform custom rendering and presentation operations.
    /// </remarks>
    [Serializable]
    [ExecuteAlways]
    public abstract class ProjectionPolicy : MonoBehaviour
    {
        [SerializeField]
        bool m_IsDebug;

        public virtual bool SupportsTestPattern => false;

        public const string TestPatternParameterId = "ShowTestPattern";

        Material m_CustomBlitMaterial;
        Dictionary<int, MaterialPropertyBlock> m_CustomBlitMaterialPropertyBlocks;
        MaterialPropertyBlock m_CustomBlitMaterialPropertyBlock;

        protected Material customBlitMaterial => m_CustomBlitMaterial;

        // Hardcoded test pattern
        [SerializeField, HideInInspector]
        Texture2D m_TestPatternTexture;

        protected MaterialPropertyBlock GetCustomBlitMaterialPropertyBlocks(int index)
        {
            if (m_CustomBlitMaterialPropertyBlocks == null)
            {
                return m_CustomBlitMaterialPropertyBlock;
            }

            if (!m_CustomBlitMaterialPropertyBlocks.TryGetValue(index, out var materialPropertyBlock))
            {
                throw new System.ArgumentException($"There is no: {nameof(MaterialPropertyBlock)} for index: {index}");
            }

            if (materialPropertyBlock == null)
            {
                throw new System.ArgumentException($"NULL: {nameof(MaterialPropertyBlock)} for index: {index}");
            }

            return materialPropertyBlock;
        }

        public void SetCustomBlitMaterial(Material material, MaterialPropertyBlock materialPropertyBlock = null)
        {
            m_CustomBlitMaterial = material;
            m_CustomBlitMaterialPropertyBlock = materialPropertyBlock;
        }

        public void SetCustomBlitMaterial(Material material, Dictionary<int, MaterialPropertyBlock> materialPropertyBlocks = null)
        {
            m_CustomBlitMaterial = material;
            m_CustomBlitMaterialPropertyBlocks = materialPropertyBlocks;
        }

        [SerializeField]
        int m_NodeIndexOverride;

        const string k_TestPatternTexturePath = "uvtestpattern";

        /// <summary>
        /// Called just before the frame is rendered.
        /// </summary>
        /// <param name="clusterSettings">The current cluster display settings.</param>
        /// <param name="activeCamera">The current "main" camera.
        /// </param>
        /// <remarks>
        /// The <paramref name="activeCamera"/> will be disabled by default so it will not be rendered
        /// normally. At this point, you can perform special logic, such as manipulating projection
        /// matrices, rendering to a <see cref="RenderTexture"/>, etc. Do not draw anything to the screen;
        /// that should happen in your <see cref="Present"/> method.
        /// </remarks>
        public abstract void UpdateCluster(ClusterRendererSettings clusterSettings, Camera activeCamera);

        /// <summary>
        /// Called after all rendering commands have been enqueued in the rendering pipeline.
        /// </summary>
        /// <param name="args">A <see cref="PresentArgs"/> struct holding present arguments.</param>
        /// <remarks>
        /// At this point, you can enqueue any commands that are required to draw the final
        /// output to the current display output device.
        /// </remarks>
        public abstract void Present(PresentArgs args);

        /// <summary>
        /// Gets or sets the origin of the cluster display.
        /// </summary>
        public virtual Matrix4x4 Origin { get; set; }

        /// <summary>
        /// Specifies whether debug mode is enabled.
        /// </summary>
        public bool IsDebug
        {
            set => m_IsDebug = value;
            get => m_IsDebug;
        }

        public int NodeIndexOverride
        {
            get => m_NodeIndexOverride;
            set => m_NodeIndexOverride = value;
        }

        protected void RenderTestPattern(CommandBuffer cmd)
        {
            if (m_TestPatternTexture == null)
            {
                m_TestPatternTexture = Resources.Load<Texture2D>(k_TestPatternTexturePath);
            }
            GraphicsUtil.Blit(cmd, m_TestPatternTexture, false);
        }

        protected void RenderTestPattern(RenderTexture target)
        {
            if (m_TestPatternTexture == null)
            {
                m_TestPatternTexture = Resources.Load<Texture2D>(k_TestPatternTexturePath);
            }

            UnityEngine.Graphics.Blit(m_TestPatternTexture, target);
        }

        protected int GetEffectiveNodeIndex() =>
            !IsDebug && ServiceLocator.TryGet(out IClusterSyncState clusterSync) &&
            clusterSync.IsClusterLogicEnabled
                ? clusterSync.RenderNodeID
                : NodeIndexOverride;
    }
}
