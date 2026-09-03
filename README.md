# Render Lab

一个以 C++、Win32 和 Direct3D 11 实现的迷你实时渲染管线学习工程。

这里不使用游戏引擎，也不尝试做通用引擎。目标是亲手完成一个可观察、可调试的场景渲染器，理解 CPU 如何提交命令，以及 GPU 如何处理资源、几何、深度、像素着色和混合。

## 最终目标

实现一个可加载小型场景的 **Forward PBR Renderer**。每帧采用固定、明确的渲染顺序：

```text
更新相机和场景
  -> CPU 视锥剔除
  -> Shadow Map Pass
  -> Opaque Forward PBR Pass
  -> Skybox Pass
  -> Transparent Pass
  -> HDR Tone Mapping
  -> Debug UI / Present
```

完成后，项目应能演示：

- 加载 Mesh、纹理、材质、相机和方向光；
- 正确的 Model / View / Projection 变换；
- 背面剔除、深度测试、深度写入和透明混合；
- CPU 视锥剔除，并显示总物体数、剔除数和实际 Draw Call；
- 金属度—粗糙度 PBR、法线贴图、HDR 与 Tone Mapping；
- 方向光 Shadow Map 与 PCF 阴影；
- 深度、阴影图、法线和最终颜色的调试视图；
- 每帧 GPU 时间统计与 RenderDoc/PIX 抓帧分析。

## 必须亲手实现的概念

| 范畴 | 必须实现 | 目标 |
| --- | --- | --- |
| CPU/GPU 协作 | Swap chain、每帧资源、命令提交、同步、资源绑定 | 明确 CPU 录制命令而 GPU 异步执行 |
| 几何管线 | 顶点/索引 Buffer、Shader、裁剪、背面剔除 | 从 Mesh 生成屏幕上的三角形 |
| 深度 | Depth buffer、Depth Test、Depth Write | 让不透明物体正确遮挡，并理解 Early-Z 的价值 |
| 混合 | Alpha Blend、透明物体从远到近排序、关闭透明物体的 Depth Write | 正确合成水、玻璃和粒子等半透明物体 |
| 材质与光照 | 金属度—粗糙度工作流、GGX、Smith、Schlick Fresnel、法线贴图 | 完成可控的 Forward PBR 光照 |
| 可见性 | Bounding Sphere/AABB、CPU Frustum Culling | 不提交相机视锥外的物体 |
| 阴影 | Directional Light Shadow Map、PCF | 让光照与场景几何产生可信遮挡 |
| 图像输出 | HDR Render Target、曝光、Tone Mapping | 区分线性色彩计算与最终屏幕输出 |
| 分析 | Timestamp Query、RenderDoc/PIX、Debug Views | 用数据而非直觉判断 GPU 工作量 |

不透明与透明物体采用不同的状态和顺序：

| 类型 | Depth test | Depth write | Blend | 顺序 |
| --- | --- | --- | --- | --- |
| Opaque | 开 | 开 | 关 | 可按材质/状态组织 |
| Skybox | 开 | 关 | 关 | 不透明物体之后 |
| Transparent | 开 | 关 | 开 | 从远到近 |

透明像素使用标准 alpha 混合：

```text
finalColor = srcColor * srcAlpha + dstColor * (1 - srcAlpha)
```

## 里程碑

1. **00 - Hello Triangle**：Win32 窗口、Direct3D 11 Device、DXGI Swap Chain、HLSL Shader 和 `Draw(3)`。
2. **01 - Frame foundation**：清屏、帧同步和窗口 resize。
3. **02 - Geometry and depth**：三角形、索引立方体、MVP 变换、Depth Buffer、背面剔除。
4. **03 - Scene and transparency**：导入 Mesh/Texture、相机、材质、Blend State、透明排序。
5. **04 - Forward PBR**：Directional Light、金属度/粗糙度、法线贴图、HDR 和 Tone Mapping。
6. **05 - Shadow and visibility**：Shadow Map、PCF、AABB/Bounding Sphere、CPU Frustum Culling。
7. **06 - Debug and profile**：Depth/Shadow/Normal 调试视图、GPU Timestamp、RenderDoc/PIX 抓帧记录。

## 有意延后的内容

以下内容很有价值，但不是第一版的完成条件：

- Deferred Rendering / GBuffer / 多光源优化；
- Render Graph；
- Hi-Z 遮挡剔除、GPU Culling、Indirect Draw；
- LOD/HLOD、Instancing、Virtual Texturing；
- SSAO、SSR、TAA、体积雾；
- IBL 预计算、光追、Lumen、Nanite；
- ECS、场景编辑器、跨 API 抽象层。

它们应在基础管线稳定后，作为单独的实验分支逐一加入。首个升级优先选择 **Hi-Z GPU Culling** 或 **Deferred GBuffer** 之一：前者偏向 Compute 与可见性，后者偏向像素、Render Target 和显存带宽。

## 验收方式

每增加一个功能，同时给出对应的可观察证据：

- 深度与背面剔除：对比开关后的画面和 GPU 时间；
- 透明混合：展示错误的 Depth Write 与正确排序的差异；
- 视锥剔除：显示 `总实例数 -> 视锥内实例数 -> 实际 Draw Call`；
- 阴影：显示 Shadow Map 和 PCF 前后的结果；
- PBR：展示 Albedo、Normal、Metallic、Roughness 与最终 HDR 颜色；
- 性能：记录每个 Pass 的 GPU ms，并用 RenderDoc/PIX 验证资源和 Draw Call。

## Setup

安装 Visual Studio 的“使用 C++ 的桌面开发”工作负载、CMake、Windows 10 或更新版本，以及当前显卡驱动。Direct3D 11 Runtime 由 Windows 提供；不要安装已废弃的 DirectX SDK。

初始工程只提供 CMake、一个空的 Win32 程序入口和一个空的 HLSL 文件。窗口、Device、Swap Chain 与绘制代码均留作练习实现。

```powershell
cmake --preset vs2022-debug
cmake --build --preset vs2022-debug
```

完成第一个里程碑后，可执行程序应显示深色背景上的红绿蓝三角形。
