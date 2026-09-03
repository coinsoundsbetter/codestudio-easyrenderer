#include "Application.h"
#include <GLFW/glfw3.h>
#define GLFW_EXPOSE_NATIVE_WIN32
#include <GLFW/glfw3native.h>

Application::Application(const std::string& title)
{
    _title = title;
}

Application::~Application()
{
    Cleanup();
}

void Application::Run()
{
    if (!Initialize())
    {
        return;
    }

    while (!glfwWindowShouldClose(_window))
    {
        glfwPollEvents();
        Update();
        Render();
    }
}

void Application::Cleanup()
{
    if (_window != nullptr)
    {
        glfwDestroyWindow(_window);
        _window = nullptr;
    }
    glfwTerminate();
}

bool Application::Load()
{
    DXGI_SWAP_CHAIN_DESC swapChainDesc{};
    swapChainDesc.BufferCount = 1;
    swapChainDesc.BufferDesc.Width = _width;
    swapChainDesc.BufferDesc.Height = _height;
    swapChainDesc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    swapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    swapChainDesc.OutputWindow = glfwGetWin32Window(_window);
    swapChainDesc.SampleDesc.Count = 1;
    swapChainDesc.Windowed = TRUE;
    swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;

    D3D_FEATURE_LEVEL featureLevel{};
    const HRESULT result = D3D11CreateDeviceAndSwapChain(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        0,
        nullptr,
        0,
        D3D11_SDK_VERSION,
        &swapChainDesc,
        _swapChain.GetAddressOf(),
        _device.GetAddressOf(),
        &featureLevel,
        _context.GetAddressOf()
    );
    if (FAILED(result))
    {
        return false;
    }

    Microsoft::WRL::ComPtr<ID3D11Texture2D> backBuffer;
    const HRESULT getBufferResult = _swapChain->GetBuffer(
        0,
        IID_PPV_ARGS(backBuffer.GetAddressOf())
    );
    if (FAILED(getBufferResult))
    {
        return false;
    }

    const HRESULT createViewResult = _device->CreateRenderTargetView(
        backBuffer.Get(),
        nullptr,
        _renderTargetView.GetAddressOf()
    );
    if (FAILED(createViewResult))
    {
        return false;
    }

    const float clearColor[] = { 0.06f, 0.11f, 0.18f, 1.0f };
    _context->OMSetRenderTargets(
        1,
        _renderTargetView.GetAddressOf(),
        nullptr);
    _context->ClearRenderTargetView(
        _renderTargetView.Get(),
        clearColor);
    _swapChain->Present(1, 0);

    return true;
}

bool Application::Initialize()
{
    if (!glfwInit())
    {
        std::cout << "GLFW: Unable to initialize\n";
        return false;
    }

    GLFWmonitor* primaryMonitor = glfwGetPrimaryMonitor();
    const GLFWvidmode* videoMode = glfwGetVideoMode(primaryMonitor);
    _width = static_cast<int32_t>(videoMode->width * 0.3f);
    _height = static_cast<int32_t>(videoMode->height * 0.3f);

    glfwWindowHint(GLFW_SCALE_TO_MONITOR, GLFW_FALSE);
    glfwWindowHint(GLFW_CLIENT_API, GLFW_NO_API);
    _window = glfwCreateWindow(_width, _height, _title.data(), nullptr, nullptr);
    if (_window == nullptr)
    {
        std::cout << "GLFW: Unable to create window\n";
        return false;
    }

    const int32_t windowLeft = videoMode->width / 2 - _width / 2;
    const int32_t windowTop = videoMode->height / 2 - _height / 2;
    glfwSetWindowPos(_window, windowLeft, windowTop);

    if (!Load()) {
        return false;
    }

    return true;
}