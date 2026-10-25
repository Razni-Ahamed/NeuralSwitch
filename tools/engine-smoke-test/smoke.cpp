// A stand-in game for testing the patched engine without a real one: a small Direct3D 12 window that
// clears and presents for a number of seconds. It imports winmm.dll (timeBeginPeriod), so the engine,
// copied beside it as winmm.dll, loads into it exactly as it does into a game.
//
//   smoke.exe [seconds]
//
// It never calls DLSS, so Neural Rendering itself never runs here; what this exercises is the engine
// loading, its overlay starting on the swapchain, and the remote-control pipe answering from it.

#define UNICODE
#define _UNICODE
#include <windows.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <timeapi.h>

#include <cstdio>
#include <cstdlib>

#pragma comment(lib, "d3d12.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "winmm.lib")
#pragma comment(lib, "user32.lib")

namespace
{
constexpr UINT FrameCount = 2;

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    if (msg == WM_DESTROY)
    {
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProcW(hwnd, msg, wp, lp);
}

bool Check(HRESULT hr, const char* what)
{
    if (FAILED(hr))
        fprintf(stderr, "%s failed: 0x%08lx\n", what, (unsigned long) hr);

    return SUCCEEDED(hr);
}
} // namespace

int main(int argc, char** argv)
{
    const int seconds = argc > 1 ? atoi(argv[1]) : 20;
    timeBeginPeriod(1);

    WNDCLASSW wc {};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.lpszClassName = L"NeuralSwitchSmoke";
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassW(&wc);

    HWND hwnd = CreateWindowW(wc.lpszClassName, L"NeuralSwitch engine smoke test", WS_OVERLAPPEDWINDOW, 80, 80, 640, 360,
                              nullptr, nullptr, wc.hInstance, nullptr);
    ShowWindow(hwnd, SW_SHOWNOACTIVATE);

    IDXGIFactory4* factory = nullptr;
    ID3D12Device* device = nullptr;
    ID3D12CommandQueue* queue = nullptr;
    IDXGISwapChain3* swapChain = nullptr;
    ID3D12DescriptorHeap* rtvHeap = nullptr;
    ID3D12Resource* targets[FrameCount] {};
    ID3D12CommandAllocator* allocator = nullptr;
    ID3D12GraphicsCommandList* list = nullptr;
    ID3D12Fence* fence = nullptr;

    if (!Check(CreateDXGIFactory1(IID_PPV_ARGS(&factory)), "CreateDXGIFactory1") ||
        !Check(D3D12CreateDevice(nullptr, D3D_FEATURE_LEVEL_12_0, IID_PPV_ARGS(&device)), "D3D12CreateDevice"))
        return 1;

    D3D12_COMMAND_QUEUE_DESC qd {};
    qd.Type = D3D12_COMMAND_LIST_TYPE_DIRECT;
    if (!Check(device->CreateCommandQueue(&qd, IID_PPV_ARGS(&queue)), "CreateCommandQueue"))
        return 1;

    // An explicit size, as games give: the engine takes a swapchain under 100 pixels (or "0, size it to the
    // window") for an overlay's and leaves it alone.
    DXGI_SWAP_CHAIN_DESC1 sd {};
    sd.Width = 1280;
    sd.Height = 720;
    sd.BufferCount = FrameCount;
    sd.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    sd.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
    sd.SampleDesc.Count = 1;

    IDXGISwapChain1* sc1 = nullptr;
    if (!Check(factory->CreateSwapChainForHwnd(queue, hwnd, &sd, nullptr, nullptr, &sc1), "CreateSwapChainForHwnd"))
        return 1;

    sc1->QueryInterface(IID_PPV_ARGS(&swapChain));
    sc1->Release();

    D3D12_DESCRIPTOR_HEAP_DESC hd {};
    hd.NumDescriptors = FrameCount;
    hd.Type = D3D12_DESCRIPTOR_HEAP_TYPE_RTV;
    device->CreateDescriptorHeap(&hd, IID_PPV_ARGS(&rtvHeap));
    const UINT rtvSize = device->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);

    for (UINT i = 0; i < FrameCount; i++)
    {
        swapChain->GetBuffer(i, IID_PPV_ARGS(&targets[i]));
        D3D12_CPU_DESCRIPTOR_HANDLE h = rtvHeap->GetCPUDescriptorHandleForHeapStart();
        h.ptr += i * rtvSize;
        device->CreateRenderTargetView(targets[i], nullptr, h);
    }

    device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&allocator));
    device->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, allocator, nullptr, IID_PPV_ARGS(&list));
    list->Close();
    device->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&fence));
    HANDLE fenceEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    UINT64 fenceValue = 0;

    printf("smoke: presenting for %d seconds (pid %lu)\n", seconds, GetCurrentProcessId());
    fflush(stdout);

    const ULONGLONG end = GetTickCount64() + (ULONGLONG) seconds * 1000;
    MSG msg {};
    UINT64 frames = 0;

    while (GetTickCount64() < end)
    {
        while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            if (msg.message == WM_QUIT)
                goto done;

            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }

        const UINT index = swapChain->GetCurrentBackBufferIndex();
        allocator->Reset();
        list->Reset(allocator, nullptr);

        D3D12_RESOURCE_BARRIER barrier {};
        barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
        barrier.Transition.pResource = targets[index];
        barrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
        barrier.Transition.StateBefore = D3D12_RESOURCE_STATE_PRESENT;
        barrier.Transition.StateAfter = D3D12_RESOURCE_STATE_RENDER_TARGET;
        list->ResourceBarrier(1, &barrier);

        D3D12_CPU_DESCRIPTOR_HANDLE rtv = rtvHeap->GetCPUDescriptorHandleForHeapStart();
        rtv.ptr += index * rtvSize;
        const float shade = 0.08f + 0.04f * (float) ((frames / 30) % 2);
        const float color[] = { shade, shade + 0.05f, shade + 0.04f, 1.0f };
        list->ClearRenderTargetView(rtv, color, 0, nullptr);

        barrier.Transition.StateBefore = D3D12_RESOURCE_STATE_RENDER_TARGET;
        barrier.Transition.StateAfter = D3D12_RESOURCE_STATE_PRESENT;
        list->ResourceBarrier(1, &barrier);
        list->Close();

        ID3D12CommandList* lists[] = { list };
        queue->ExecuteCommandLists(1, lists);
        swapChain->Present(1, 0);

        // Simple and slow: wait for the GPU every frame. Enough for a test.
        queue->Signal(fence, ++fenceValue);
        if (fence->GetCompletedValue() < fenceValue)
        {
            fence->SetEventOnCompletion(fenceValue, fenceEvent);
            WaitForSingleObject(fenceEvent, 1000);
        }

        frames++;
    }

done:
    printf("smoke: presented %llu frames\n", frames);
    timeEndPeriod(1);
    return 0;
}
