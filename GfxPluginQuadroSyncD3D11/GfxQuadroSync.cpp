#include "d3d11.h"
#include "dxgi.h"

#include "D3D11QuadroSync.h"
#include "GfxQuadroSync.h"

#include "Unity/IUnityRenderingExtensions.h"
#include "Unity/IUnityGraphicsD3D11.h"

namespace GfxQuadroSync
{
static PluginCSwapGroupClient s_SwapGroupClient;
static IUnityInterfaces* s_UnityInterfaces = NULL;
static IUnityGraphicsD3D11* s_UnityGraphicsD3D11 = NULL;
static IUnityGraphics* s_UnityGraphics = NULL;
static ID3D11Device* s_D3D11Device = NULL;
static IDXGISwapChain* s_D3D11SwapChain = NULL;
static bool s_Initialized = false;

// Override the function defining the load of the plugin
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityPluginLoad(IUnityInterfaces * unityInterfaces)
{
    if (unityInterfaces)
    {
        s_UnityInterfaces = unityInterfaces;
        s_UnityGraphics = s_UnityInterfaces->Get<IUnityGraphics>();
        if (s_UnityGraphics)
        {
            s_UnityGraphicsD3D11 = unityInterfaces->Get<IUnityGraphicsD3D11>();
            s_D3D11Device = s_UnityGraphicsD3D11->GetDevice();
            s_D3D11SwapChain = s_UnityGraphicsD3D11->GetSwapChain();
            s_UnityGraphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
        }
    }
}

// Freely defined function to pass a callback to plugin-specific scripts
extern "C" UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
GetRenderEventFuncD3D11()
{
    return OnRenderEvent;
}

// Override the query method to use the `PresentFrame` callback
// It has been added specially for the Quadro Sync system
extern "C" bool UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API
UnityRenderingExtQuery(UnityRenderingExtQueryType query)
{
    if (!IsContextValid())
        return false;

    return (query == UnityRenderingExtQueryType::kUnityRenderingExtQueryOverridePresentFrame)
        ? s_SwapGroupClient.Render(s_D3D11Device,
                                   s_D3D11SwapChain,
                                   s_UnityGraphicsD3D11->GetSyncIntervalImpl(),
                                   s_UnityGraphicsD3D11->GetPresentFlagsImpl())
        : false;
}

// Override function to receive graphics event 
static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    if (eventType == kUnityGfxDeviceEventInitialize && !s_Initialized)
    {
        s_Initialized = true;
        s_SwapGroupClient.Prepare();
    }
    else if (eventType == kUnityGfxDeviceEventShutdown)
    {
        s_Initialized = false;
    }
}

// Plugin function to handle a specific rendering event
static void UNITY_INTERFACE_API
OnRenderEvent(int eventID, void* data)
{
    switch (eventID)
    {
        case (int)EQuadroSyncRenderEvent::QuadroSyncInitialize:
            QuadroSyncInitialize();
            break;
        case (int)EQuadroSyncRenderEvent::QuadroSyncQueryFrameCount:
            QuadroSyncQueryFrameCount(static_cast<int* const>(data));
            break;
        case (int)EQuadroSyncRenderEvent::QuadroSyncResetFrameCount:
            QuadroSyncResetFrameCount();
            break;
        case (int)EQuadroSyncRenderEvent::QuadroSyncDispose:
            QuadroSyncDispose();
            break;
        case (int)EQuadroSyncRenderEvent::QuadroSyncEnableSystem:
            QuadroSyncEnableSystem(static_cast<bool>(data));
            break;
        case (int)EQuadroSyncRenderEvent::QuadroSyncEnableSwapGroup:
            QuadroSyncEnableSwapGroup(static_cast<bool>(data));
            break;
        case (int)EQuadroSyncRenderEvent::QuadroSyncEnableSwapBarrier:
            QuadroSyncEnableSwapBarrier(static_cast<bool>(data));
            break;
        case (int)EQuadroSyncRenderEvent::QuadroSyncEnableSyncCounter:
            QuadroSyncEnableSyncCounter(static_cast<bool>(data));
            break;
        default:
            break;
    }
}

// Verify if the D3D11 Device and the Swap Chain are valid
bool IsContextValid()
{
    if (s_UnityGraphics->GetRenderer() != UnityGfxRenderer::kUnityGfxRendererD3D11)
        return false;
	
    if (s_D3D11Device == nullptr)
        s_D3D11Device = s_UnityGraphicsD3D11->GetDevice();

    if (s_D3D11SwapChain == nullptr)
        s_D3D11SwapChain = s_UnityGraphicsD3D11->GetSwapChain();

    return (s_D3D11Device != nullptr && s_D3D11SwapChain != nullptr);
}

// Enable Workstation SwapGroup & potentially join the SwapGroup / Barrier
void QuadroSyncInitialize()
{
    if (!IsContextValid())
        return;

    s_SwapGroupClient.SetupWorkStation();
    s_SwapGroupClient.Initialize(s_D3D11Device, s_D3D11SwapChain);
}

// Query the actual frame count (master or custom one)
void QuadroSyncQueryFrameCount(int* const value)
{
    if (!IsContextValid() || value == nullptr)
        return;

    auto frameCount = s_SwapGroupClient.QueryFrameCount(s_D3D11Device);
    *value = (int)frameCount;
}

// Reset the frame count (master or custom one)
void QuadroSyncResetFrameCount()
{
    if (!IsContextValid())
        return;

    s_SwapGroupClient.ResetFrameCount(s_D3D11Device);
}

// Leave the Barrier and Swap Group, disable the Workstation SwapGroup
void QuadroSyncDispose()
{
    if (!IsContextValid())
        return;

    s_SwapGroupClient.Dispose(s_D3D11Device, s_D3D11SwapChain);
    s_SwapGroupClient.DisposeWorkStation();
}

// Directly join or leave the Swap Group and Barrier
void QuadroSyncEnableSystem(const bool value)
{
    if (!IsContextValid())
        return;

    s_SwapGroupClient.EnableSystem(s_D3D11Device, s_D3D11SwapChain, value);
}

// Toggle to join/leave the SwapGroup
void QuadroSyncEnableSwapGroup(const bool value)
{
    if (!IsContextValid())
        return;

    s_SwapGroupClient.EnableSwapGroup(s_D3D11Device, s_D3D11SwapChain, value);
}

// Toggle to join/leave the Barrier
void QuadroSyncEnableSwapBarrier(const bool value)
{
    if (!IsContextValid())
        return;

    s_SwapGroupClient.EnableSwapBarrier(s_D3D11Device, value);
}

// Enable or disable the Master Sync Counter
void QuadroSyncEnableSyncCounter(const bool value)
{
    if (!IsContextValid())
        return;

    s_SwapGroupClient.EnableSyncCounter(value);
}

}