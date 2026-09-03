//
// Created by linjiaxin on 2026/9/3.
//

#ifndef RENDER_LAB_APPLICATION_H
#define RENDER_LAB_APPLICATION_H

#include <string>
#include <iostream>
#include <d3d11.h>
#include <wrl/client.h>

struct GLFWwindow;

class Application
{
public:
    Application(const std::string& title);
    virtual  ~Application();
    void Run();

protected:
    virtual void Cleanup();
    virtual bool Initialize();
    virtual bool Load();
    virtual void Render() = 0;
    virtual void Update() = 0;

    Microsoft::WRL::ComPtr<ID3D11Device> _device; //创建资源的GPU设备
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> _context; //提交命令的上下文
    Microsoft::WRL::ComPtr<IDXGISwapChain> _swapChain; //窗口画布管理器
    Microsoft::WRL::ComPtr<ID3D11RenderTargetView> _renderTargetView; //当前可写画布

private:
    GLFWwindow* _window = nullptr;
    int32_t _width = 0;
    int32_t _height = 0;
    std::string _title;
};


#endif //RENDER_LAB_APPLICATION_H
