# 第 3 章：图形处理器（GPU）

> 来源：《Real-Time Rendering, Fourth Edition》Chapter 3
> 学习状态：已完成首次阅读

## 一句话概括

GPU 追求的是高吞吐量：它把大量相似的 shader 调用组成 warp 并行执行，并在某一批等待内存时快速执行另一批。传统图形阶段把三角形转成图像；Compute Shader 则让 GPU 处理更一般的并行任务。

## 3.1 数据并行架构

- CPU 主要追求低延迟，依赖缓存、分支预测、乱序执行等处理复杂逻辑；GPU 主要追求高吞吐，使用大量 shader core 处理大量相似数据。
- 一次 Vertex/Pixel Shader 调用可视作一个 GPU 线程；它不是操作系统线程。
- NVIDIA 的 **warp** 通常有 32 个线程；AMD 的 **wavefront** 常见为 32 或 64 个。它们近似锁步执行同一条指令、各自处理不同数据。
- 纹理读取等操作等待时，GPU 暂停当前 warp，立即执行另一个已就绪 warp；原请求返回后再恢复。这是 **延迟隐藏（latency hiding）**，并非让内存访问没有延迟。
- `occupancy` 是可同时驻留、可被调度的 warp 供给程度。寄存器和共享内存使用过多会减少可驻留 warp；但缓存、带宽、分支与实际指令同样影响性能。
- 同一 warp 内线程走不同 `if`/循环路径会发生 **线程分歧（thread divergence）**：多条路径通常需轮流执行，部分 lane 暂时闲置。

```text
warp A 请求纹理 → 暂停 A
warp B 执行其他像素的计算
warp C 执行
A 的纹理数据返回 → A 继续
```

## 3.2 GPU 管线总览

```text
Vertex Shader（可编程）
  → Tessellation（可选）
  → Geometry Shader（可选）
  → 裁剪 / 三角形设置 / 遍历（固定功能）
  → Pixel Shader（可编程）
  → Merging / Output Merger（可配置）
```

| 控制类别 | 含义 | 例子 |
| --- | --- | --- |
| 可编程 | 开发者编写 shader 决定算法 | Vertex、Pixel Shader |
| 可配置 | 选择规则和参数，不能写该阶段算法 | 深度、模板、混合、剔除 |
| 固定功能 | API 不直接提供自定义算法 | 裁剪、三角形遍历 |

- **逻辑模型**是 DirectX/Vulkan 向程序员暴露的接口；**物理模型**是驱动和硬件实际如何调度、拆分、合并工作，两者不必一一对应。
- DX12 的 PSO、Vulkan 的 graphics pipeline 组合 shader 和大量可配置状态，但不等于芯片中存在同样划分的独立硬件块。

## 3.3 可编程 Shader 阶段

- 统一着色器架构让同一池 shader core 可按负载执行不同种类的 shader，而非永久划分顶点或像素核心。
- HLSL/GLSL 等源码通常经中间表示（如 DXIL、SPIR-V）和驱动/离线编译，转为特定 GPU 的指令。

| 类型 | 含义 | 例子 |
| --- | --- | --- |
| uniform | 同一次 draw 的所有调用共享 | 相机矩阵、光源、材质参数、绑定纹理 |
| varying | 每个顶点或片元不同 | 位置、UV、法线、顶点色 |
| temporary | 单个 shader 调用私有的中间值 | 采样颜色、归一化法线、光照强度 |

- Vertex Shader 的输出经光栅化插值，常成为 Pixel Shader 的 varying 输入。
- uniform 决定的分支在整个 draw 内一致，通常不会造成 warp 分歧；varying 决定的动态分支更灵活，但可能降低效率。

## 3.4 可编程着色与 API 的演进

- 早期固定功能 GPU 只能按预设组合纹理、颜色与光照；复杂效果常需要多 pass 拼合。
- 可编程 shader 让开发者定义顶点与像素计算；Shade Tree 的思想延续为现代材质节点图。
- Shader Model 的演进带来了真正可编程的顶点/像素 shader、HLSL、动态分支、统一 shader、Geometry Shader、Tessellation 与 Compute Shader。
- DX12、Vulkan、Metal 的主要目标是降低驱动开销、提高多核 CPU 提交能力，并把资源状态和同步管理更明确地交给引擎；它们不保证自动更快，复杂度也更高。

## 3.5 顶点着色器（Vertex Shader）

```text
顶点/索引缓冲
  → Input Assembler 组装顶点与图元
  → Vertex Shader 逐顶点独立执行
  → 光栅化
```

- Input Assembler 从顶点流和索引组装图元，并支持 instancing。
- Vertex Shader 每次只处理一个顶点：看不见同一三角形的其他顶点，不能创建或删除几何顶点。
- 它必须输出裁剪空间位置：`clipPosition = Projection × View × Model × localPosition`。
- 它还可输出 UV、法线、世界坐标、顶点色等，供之后插值并传给 Pixel Shader。
- 平滑顶点法线让粗网格呈现连续曲面；要表现锐利折痕，通常在同一位置复制顶点并赋予两侧不同法线。
- 骨骼动画、morph、旗帜/水面形变、地形高度与全屏扭曲都是典型用途。
- Instancing 可让同一网格带不同模型矩阵、颜色或透明度多次绘制，减少 draw call。

## 3.6 曲面细分阶段（Tessellation）

```text
Patch 控制点
  → Hull Shader：处理控制点、决定细分因子
  → 固定功能 Tessellator：生成新顶点与图元拓扑
  → Domain Shader：计算新顶点的位置、法线、UV 等
```

- Tessellation 根据距离、屏幕大小或质量目标动态增加三角形，实现连续 LOD。
- Tessellator 只增加顶点和图元，**不会自动把平面变圆**；Domain Shader 还需用曲面方程、高度图或位移贴图决定新顶点位置。
- `outer factor` 控制边缘切分，`inner factor` 控制内部密度。
- 相邻 patch 的共享边必须使用相同 outer factor，否则边缘顶点无法对齐并产生裂缝。
- 地形常由粗网格 patch 构成：近处提高细分、远处降低细分，再用高度图定位新顶点。传统四叉树 LOD、边缘拼接、geomorph、skirt 也是替代方案。

## 3.7 Geometry Shader 与 Stream Output

- Geometry Shader（GS）一次接收一个点、线或三角形，因此能看见图元的全部顶点。
- GS 可以输出零个或多个图元：删除图元、生成线框边、从点生成 billboard、有限复制到 cube map 面等。
- 它需保持图元输出顺序，且每个输入图元的输出数量不确定，削弱并行效率；现代引擎通常较少用它做大规模几何生成，常改用 instancing、Compute Shader 或 Mesh Shader。
- **Stream Output**（OpenGL：Transform Feedback）可将 Vertex/Tessellation/Geometry 阶段输出按顺序写入 GPU 缓冲，供后续处理或再次输入管线，避免 GPU→CPU→GPU 往返。完整三角形输出会失去原始共享顶点关系并造成重复数据。

## 3.8 Pixel Shader

```text
三角形覆盖屏幕位置
  → Rasterizer 生成 fragment、插值顶点输出
  → Pixel Shader 计算颜色/Alpha/可选深度
  → Merging Stage 决定最终写入
```

- Pixel Shader（OpenGL 常称 Fragment Shader）处理的是 fragment，不保证每个结果最终成为屏幕像素。
- UV、法线、世界坐标等顶点输出一般通过**透视正确插值**，成为每个 fragment 不同的输入。
- Pixel Shader 可输出颜色、Alpha、多个 render target，也可修改深度或 `discard` 当前 fragment。
- `discard` / `clip` 用于 Alpha Test：树叶、栅栏等区域要么完全丢弃、要么完全保留，不等同于半透明混合。
- **MRT** 可一次写入多份数据，如基础颜色、法线、材质参数、ID，是延迟渲染的重要基础。
- 普通 Pixel Shader 主要写自己的 fragment 位置，不能可靠读取邻居刚写出的结果；图像滤波通常读取前一 pass 的完整输出。
- GPU 以 2×2 的 **quad** 处理相邻 fragment，可得到 `ddx`/`ddy` 变化率；纹理采样据此选择合适 mipmap。严重动态分支分歧中不应依赖隐式导数。
- UAV/SSBO 允许 shader 读写任意共享位置；并行写同一位置会产生 data race，需要 atomic，代价是竞争与等待。

## 3.9 合并阶段（Merging Stage）

- DirectX 常称 Output Merger；它将 Pixel Shader 的颜色和深度，与 framebuffer 中已有数据结合。
- 它负责深度测试、模板测试、裁剪区域测试、颜色混合及颜色/深度/模板写入。

| 类型 | 深度测试 | 深度写入 | 混合 | 常见顺序 |
| --- | --- | --- | --- | --- |
| 不透明物体 | 开 | 开 | 关，直接覆盖 | 通常近到远，帮助 Early-Z |
| 普通半透明物体 | 通常开 | 通常关 | 开 | 通常远到近，保证混合正确 |
| Alpha Test / Clip 物体 | 通常开 | 通常开 | 关 | 可按不透明物体处理 |

- 标准 alpha 混合：`最终颜色 = 源颜色 × 源 Alpha + 目标颜色 × (1 - 源 Alpha)`。
- Pixel Shader 提供源颜色/Alpha；目标颜色来自 framebuffer，由合并阶段读取并混合。
- **Early-Z** 可在 Pixel Shader 前提前淘汰已被遮挡的 fragment，避免无效着色。Pixel Shader 若可能改变深度或 `discard`，Early-Z 通常会受限。
- Pixel Shader 可并行、乱序完成，但合并阶段保证输出遵守 draw 与图元输入顺序；这使透明物体能按提交顺序混合。

## 3.10 Compute Shader

```text
输入纹理 / 缓冲
  → Dispatch 多个 Thread Group
  → 每组线程并行计算、可共享小块内存
  → 输出到纹理 / 缓冲 / 间接绘制数据
```

- Compute Shader 复用统一 shader core，但不受三角形→光栅化→像素的传统流程约束；程序员通过 Dispatch 决定任务规模和线程索引。
- 线程按 Thread Group 组织，可用共享内存与组内同步。书中以 DirectX 11 为例，每组 1–1024 线程；现代限制因 API 与硬件而异。
- Pixel Shader 的工作域由三角形覆盖区域决定；Compute Shader 的工作域由程序员定义。
- 常见用途：后处理、模糊与曝光统计、粒子/毛发/水面模拟、GPU 剔除、间接绘制命令、网格处理、阴影与图像过滤。
- 性能原则：尽可能让中间数据留在 GPU，避免 GPU→CPU→GPU 传输。

## 易混概念

| 概念 | 含义 |
| --- | --- |
| 延迟 vs 吞吐 | 延迟是单次操作等多久；吞吐是单位时间完成多少工作。GPU 优先吞吐。 |
| warp/wavefront vs CPU 线程 | 是共同调度的一组 GPU shader 调用，不是操作系统线程。 |
| uniform / varying / temporary | 全体共享任务规则 / 每次调用不同的输入 / 单次调用私有的中间草稿。 |
| 纹理与采样颜色 | 绑定纹理资源通常是 uniform；按不同 UV 得到的采样颜色是每次调用的计算结果。 |
| Alpha Test vs Alpha Blend | 前者是丢弃或保留，通常可写深度；后者是连续混合，通常需排序且不写深度。 |
| Depth Test vs Depth Write | 测试决定 fragment 是否被既有表面遮挡；写入决定它是否成为之后的遮挡者。 |
| Vertex Shader vs Geometry Shader | VS 逐顶点、不理解图元；GS 逐图元、能有限删除或生成图元，但性能特性较差。 |
| Tessellation vs 单纯平滑 | Tessellation 增加图元；是否弯曲或位移取决于 Domain Shader 的计算。 |
| Pixel Shader vs Merging Stage | 前者计算候选属性；后者按深度、模板和混合规则修改 framebuffer。 |
| Pixel Shader vs Compute Shader | 前者由光栅化驱动；后者由 Dispatch 驱动，工作域和读写更自由。 |

## 问答记录

### Q1：warp 等待纹理时，GPU 如何切换到其他工作？

**答：** warp 的寄存器和当前指令位置已经驻留在 GPU 上。纹理读取尚未返回时，调度器只需让 shader core 执行另一个就绪 warp；这不是 CPU 那种昂贵的上下文切换。

### Q2：uniform、varying、temporary 如何用通俗方式区分？

**答：** 把同一次 draw 看成很多小画师给物体上色：uniform 是全体共享的任务规则（纹理、光源、材质参数）；varying 是每位画师的个人资料（UV、法线、位置）；temporary 是单次计算过程的草稿（采样颜色、光照强度）。

### Q3：不同材质或纹理能否使用一个 draw call？

**答：** 基础做法中，一次 draw 绑定一组 shader、资源和状态，因此不同材质通常拆成多个 draw。高级做法可用材质参数表、纹理 atlas/array 或 bindless 资源表，以 `materialId` 选择资源并合批；不同 shader、混合或深度/模板状态仍常需拆批。

### Q4：透明物体是否每个都要单独绘制？谁做排序？

**答：** 不必。共享 shader、混合状态和资源方式，并且不破坏远到近顺序的一批透明图元可合成一个或少量 draw。通常 CPU 的应用阶段按距离/排序键从远到近排列并提交；海量粒子也可用 GPU Compute 排序。相交透明物体不存在全局完美顺序。

### Q5：透明物体为什么 Depth Test 开启、Depth Write 关闭？

**答：** Depth Test 阻止透明物体显示在已经画好的不透明墙壁或地面前；Depth Write 关闭则避免先画到的一层透明物体把后面一层透明物体完全挡掉。透明物体通常从远到近混合。

### Q6：GPU 管线里不是有 Alpha Test 吗？

**答：** Alpha Test/Clip 解决“这个像素是否存在”：低于阈值就 `discard`，高于阈值则完全保留，适合树叶和镂空栅栏。它不等于玻璃需要的连续 alpha blending；后者才通常需要远到近排序。

### Q7：两个透明物体绘制时，uniform、varying、temporary 分别是什么？

**答：** 若远处蓝玻璃和近处红玻璃分别 draw，颜色、透明度、模型矩阵是各自 draw 的 uniform；UV、法线、片元位置是 varying；采样纹理色、光照结果和最终源颜色是 temporary。红玻璃不必直接知道蓝玻璃；合并阶段将它与 framebuffer 中已有结果混合。

### Q8：两件透明物体纹理相同、仅颜色和透明度不同，能否合批？

**答：** 可以更容易合批。共享纹理可保持一次绑定，颜色和透明度可作为 per-instance 数据或以 material ID 查询的参数。透明物体仍需保持远到近顺序。

### Q9：地面网格简化能使用 Tessellation 吗？

**答：** 可以。将地形切为 patch，近处提高细分、远处降低细分，再由 Domain Shader 用高度图确定新顶点高度。相邻 patch 的共享边必须采用相同 outer factor，否则顶点无法对齐并出现接缝。

## 后续重点复习

- [ ] 用一个具体材质例子复盘 draw call、shader、纹理、材质参数、uniform/varying/per-instance data 的关系。
- [ ] 对比不透明、Alpha Test 与 Alpha Blend 的 Depth Test、Depth Write、Blend 状态和典型排序。
- [ ] 用时间线再次理解 warp 等待纹理、切换和恢复，区分 latency hiding 与“纹理读取无延迟”。
- [ ] 复习透视正确插值：UV 为什么不能简单线性插值。
- [ ] 学习 mipmap、纹理 LOD 与 `ddx`/`ddy` 的关系。
- [ ] 理解 2×2 quad、邻居 fragment 与动态分支中导数受限的原因。
- [ ] 复习 Early-Z：不透明物体为何常近到远，以及 `discard`/写深度为何可能限制早期剔除。
- [ ] 继续结合实践比较 Tessellation、Compute Shader、Geometry Shader 与 Mesh Shader 的适用场景。
