﻿using System;
using System.Collections;
using UnityEngine;
using NUnit.Framework;
using Unity.ClusterDisplay.Graphics.Tests;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class TiledProjectionCustomRenderFeatureTests : ClusterRendererTest
{
    [OneTimeSetUp]
    public void LoadScene()
    {
        SceneManager.LoadScene("TiledProjectionCustomRenderFeature");
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedCluster()
    {
        yield return RenderAndCompare(() =>
        {
            Assert.IsTrue(m_ClusterRenderer.Settings.OverScanInPixels == 0, "Expected zero overscan.");
        });
    }

    [UnityTest]
    public IEnumerator CompareVanillaAndStitchedClusterWithOverscan()
    {
        yield return RenderAndCompare(() =>
        {
            m_ClusterRenderer.Settings.OverScanInPixels = 64;
        });
    }
}
