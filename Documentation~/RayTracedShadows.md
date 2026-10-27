# 原生 DXR 软阴影

本功能为矩形、方向、点和聚光灯生成屏幕空间软阴影系数，提供材质调用接口。四种灯共享场景加速结构、投影材质协议、追踪和降噪流程。

OpenPBR Balanced 和 High 已接入 RT 阴影及矩形面光源受光。有效 RT 结果会替换对应原生灯的实时阴影来源，矩形灯的各层光照共用一次 RT 可见度。包内 PBR、SpecularPBR 已支持投影，其受光仍使用原有阴影路径。

## 开启步骤

1. 使用支持原生 DXR 的 Windows DX12 设备和 Forward / Forward+ Renderer，在 Renderer 上配置 `TsukuyomiFeature` 与 Pipeline Profile。
2. 在 Profile 中打开 `EnableRayTracedShadows`。默认每像素、每盏灯 8 条射线，使用当前相机的完整渲染分辨率；`MaxRayTracedShadowLights` 默认 4，范围 1～16。这个预算由四种灯共同使用。
3. 按下面的表设置需要追踪的灯，令 `ShadowMode = RayTraced`。灯光组件默认 `Off`，保留现有场景行为。
4. 投影物使用支持的材质，Renderer 的 Cast Shadows 不能为 Off，并将 `Renderer.rayTracingMode` 设置为适合其运动方式的值。
5. 接收物使用 OpenPBR Balanced / High 材质，或在自定义受光代码中调用下面的阴影接口。接收表面须对应相机深度纹理，并提供正常的 Depth、DepthNormals 和 MotionVectors Pass。

项目中的永久示例场景为 `Assets/Examples/Tsukuyomi/OpenPBRLighting/OpenPBRLighting.unity`。材质分层、矩形 LTC 的近似范围见 [OpenPBR 受光与示例](RectLight.md#openpbr-受光与示例)。原生附加灯需要 URP **Additional Lights = Per Pixel** 才会进入逐灯 RT 接收；Per Vertex 保留原有漫反射近似。

| 光源 | 设置位置 | 半影形状与控制 |
|---|---|---|
| 矩形 | `TsukuyomiRectLight` | 使用已有世界宽高；`ShadowStrength` 控制阴影强度 |
| 方向 | 原生 `Light` + `TsukuyomiRayTracedShadowLight` | `AngularDiameter` 为角直径，默认 0.5°；0 为硬阴影 |
| 点 | 原生 `Light` + `TsukuyomiRayTracedShadowLight` | `SourceRadius` 为球形光源的世界半径，默认 0.05；0 为硬阴影 |
| 聚光 | 原生 `Light` + `TsukuyomiRayTracedShadowLight` | `SourceRadius` 为发光圆盘的世界半径，默认 0.05；保留原生锥角和范围衰减 |

原生 Light 需要开启 Shadows，强度使用 `Light.shadowStrength`；Baked 灯不参与。所有灯均可用 `ShadowPriority` 设置预算优先级。矩形灯仍受 Rect Lights 的总预算约束。

`TsukuyomiRP/Ray Traced Shadows` Volume 可覆盖总开关、采样数、灯数、投影物 Layer Mask 和方向光追踪距离。默认方向光追踪距离为 100 世界单位；超过该距离的遮挡物不在射线范围内。投影物 Layer Mask 与接收物的 Rendering Layer 是不同设置。

### 投影物与材质

原生 Renderer 的 Ray Tracing Mode 可通过 Debug Inspector 或脚本设置：静态几何用 `Static`，仅变换的物体用 `DynamicTransform`，蒙皮或变形网格用 `DynamicGeometry`。`Off` 不进入加速结构。本功能不会批量改写场景 Renderer 配置。

屏幕外遮挡物仍会被收集。屏幕外蒙皮角色还需要原生动画和 SkinnedMeshRenderer 持续更新，例如启用 `updateWhenOffscreen` 并按需配置 Animator Culling Mode；本功能不会代替 Unity 更新已经暂停的动画姿态。

已包含投影 Pass 的材质：`TsukuyomiRP/Lit/PBR`、`TsukuyomiRP/Lit/SpecularPBR`、OpenPBR Balanced 和 High。不透明材质直接遮挡；Alpha Clip 使用 UV0、`_BaseMap_ST`、Base Map Alpha、Base Color Alpha 和 Cutoff。命中着色器使用显式 LOD 0 采样 Alpha Mask。

材质 `_Cull` 控制投影正反面；Renderer 的 `ShadowCastingMode.TwoSided` 覆盖材质裁面。此标志存放在原生 `InstanceID()` 的最低位，由共享 AnyHit 直接读取；不需要额外的投影物标志缓冲。透明混合材质跳过，当前没有有色透射或半透明阴影。

自定义投影 Shader 需要：

- SubShader 标签 `"TsukuyomiRTShadow" = "True"`。
- 名称和 LightMode 都为 `TsukuyomiRTShadow` 的原生 Ray Tracing Pass。
- 与普通 Pass 一致的 `UnityPerMaterial` 布局；可在材质 Input 后包含 `Shaders/Ray Traced Shadow/RTShadowCaster.hlsl`，并提供其中使用的 Base Map、Base Color、Cutoff 和 Cull 属性。

AnyHit 负责裁面和 Alpha Clip，场景加速结构不能为这些子网格启用 `ClosestHitOnly` 或强制跳过 AnyHit。自定义顶点动画需要单独提供匹配的追踪几何；普通顶点 Shader 的位移不会自动应用到加速结构。

## 阴影采样接口

包含 `Shaders/Ray Traced Shadow/RTShadowSampling.hlsl`，公开接口返回是否具有当前相机的有效 RT 阴影：

```hlsl
bool TrySampleMainLightRTShadow(float2 screenUV, out float visibility);
bool TrySampleAdditionalLightRTShadow(uint lightLoopIndex, float2 screenUV, out float visibility);
bool TrySampleRectLightRTShadow(uint rectLightIndex, float2 screenUV, out float visibility);
```

`screenUV` 是当前渲染目标的归一化屏幕坐标。返回 `true` 时，`visibility` 已经应用该灯的 Shadow Strength：1 表示无遮挡，0 表示完全遮挡。未开启、超预算、资源不可用、没有映射或 UV 越界时返回 `false`，同时将 `visibility` 置为 1。调用方在返回 `false` 时保留原来的阴影路径。

`lightLoopIndex` 必须与 `GetAdditionalLight` 的循环参数相同：普通 Forward 在接口内部通过 `GetPerObjectLightIndex` 转换；Forward+ 直接使用灯数据索引，包括附加方向光循环。不要传入 CPU `visibleLights` 索引，也不要在调用前再次做普通 Forward 的索引转换。

矩形灯接口接收当前 `AccumulateRectLights` 灯列表的索引，与原生 Light 的索引空间独立。其阴影为几何可见度近似，diffuse 与 LTC specular 共用同一个系数，各乘一次；切换 Low / Medium / High 漫反射质量不改变阴影采样模型。

`RectLight.hlsl` 还提供直接累加带阴影光照的重载：

```hlsl
AccumulateRectLights(positionWS, normalWS, viewWS, renderingLayer,
    baseColor, metallic, roughness, multiScatter, screenUV,
    diffuse, specular);
```

该重载已应用阴影，无需再次调用 `TrySampleRectLightRTShadow` 相乘。原有不传 `screenUV` 的重载继续输出无阴影光照。

### 与已有阴影合成

原生 Light 的 RT 结果应替换该灯的**实时阴影来源**，再执行原有 ShadowMask / 烘焙阴影和距离淡出的合成。不要直接对 `GetMainLight` / `GetAdditionalLight` 返回的完整 `shadowAttenuation` 再乘 RT，因为该值可能已含实时和烘焙遮挡。

主方向光的现有 PCSS、Contact Shadow 和 Per Object Shadow 都写入主光屏幕阴影。OpenPBR 在 RT 结果有效时直接使用该结果，不再采样或叠乘这些实时阴影；RT 不可用时回退到原路径。接入层保留烘焙 ShadowMask 合成、Light Cookie 和 Rendering Layer。现有阴影功能开关及主光屏幕阴影纹理仍可供其他材质使用。

OpenPBR 的 `_RECEIVE_SHADOWS_OFF` 会禁用 RT 接收。矩形灯的 diffuse、base specular、coat 和 fuzz 合成后应用一次 RT 可见度；不要在材质末尾再次乘同一阴影。

没有对应 raster shadow keyword 时，RT 使用独立发布的距离淡出参数，仍按 URP Asset 的 Shadow Distance 和 Cascade Border 合成烘焙阴影，不读取其他相机残留的阴影参数。Shadow Distance 为 0 时，显式开启的 RT 保持有效且不做距离淡出。

透明接收表面、体积雾和其他离屏采样不能直接使用不透明表面的屏幕阴影；保留它们原有的阴影路径。

## 限制与验证

- 当前面向桌面 Game / Scene View、Forward / Forward+。Deferred、XR、透明透射、Terrain、VFX 和自定义顶点位移不在当前接入范围内。
- 无 DXR 能力、资源缺失或功能关闭时，阴影接口无有效结果。原有光照及非 RT 阴影由调用方保留。
- 有限光源的通用阴影系数不等同于针对不同 BRDF 分别积分的完全遮挡光照；粗糙度不同的高光仍共享几何可见度。
- 全分辨率、多灯和动态几何更新都会增加开销。8 条射线、4 盏灯是默认预算，不代表已达成某个 4K 帧率指标。
- 验证时覆盖 Alpha Clip 孔洞、Cull Front/Back/Off、Renderer TwoSided、屏幕外投影物、移动/蒙皮、相机切换和四种光混合。验证场景、材质及压力测试产物不作为功能资源保留。

矩形光照模型和 LTC 数据说明见 [实时矩形面光源](RectLight.md)。

## RT 生成阶段验证与性能参考

### OpenPBR 接收验证

2026-10-05，在同一 DX12 / RTX 4090 环境中完成 34 项 OpenPBR 接收检查，全部通过。关闭原生 ShadowMap 后分别验证了 Balanced 和 High 对矩形、方向、点光及聚光的 RT 接收。完全遮挡时直接光降为 0；关闭 RT 或关闭材质接收阴影后恢复无遮挡亮度，差异小于 0.05%。同时检查了矩形漫反射切档时的高光一致性、IOR 为 1、关闭高光及镜像切线。像素读回在当前 Shader 变体同步编译完成后进行；C# 与 Shader 均无编译错误。

在 `Assets/Examples/Tsukuyomi/OpenPBRLighting/OpenPBRLighting.unity` 中关闭全部 3 盏原生灯，仅保留矩形灯进行 640×360 对照。RT 分配 1 层阴影，矩形 GPU 索引为 0，加速结构包含 17 个实例；读回的阴影可见度范围为 0～1。宽度 4.4、高度 1.6 时，阴影主要落在底座及后方地面，深色材质使半影较不明显。

示例相机带有 `MainCamera` 标签，使 Scene View 能继承其 layer-30 Volume mask。若与其他 MainCamera 场景叠加，使用示例 Game View 检查；Scene View 可能继承另一个相机的 Volume 设置。

示例 Layer 30 必须命名为 `TsukuyomiExample`，Volume 的 Caster Mask 选择该层。Unity 6000.5 的内置 Volume LayerMask 绘制器只保留已命名层，会在 Inspector 重绘时把仅含未命名层的掩码写成 Nothing。正常 3840×2160 Game 帧的 CPU 状态检查曾发现 RT 已启用、矩形阴影槽为 0，但有效 Caster Mask 为 0、投影物数量为 0；命名示例层并恢复掩码后，正常 Game 帧恢复收集 17 个投影物。仅检查保存的 Volume 资产或使用不更新 Volume 的 SingleCameraRequest，不能代替这一实际相机检查。

关闭 RT 或移除全部矩形灯时，RenderGraph 会在当前相机的命令流中绑定有效的空缓冲，避免 D3D12 因缺少 SRV 跳过 OpenPBR 绘制。

### 正常 Game / Scene View 帧的接入约束

2026-10-05，在上述示例中同时显示 Game 与 Scene View，关闭全部原生灯，仅保留矩形灯，定位了四项独立问题。离屏 `SingleCameraRequest` 检查不能替代这些正常相机帧的验证。

- **追踪必须在深度复制完成后执行。** Unity 6000.5 / URP 17.5 的 Forward / Forward+ 启用 Depth Priming 时，预通道写入深度附件，需要再复制到 `cameraDepthTexture`。原先 RT 事件为 `BeforeRenderingOpaques - 1`（249），URP 将复制安排在 After GBuffer，但 Forward 路径不执行该复制分支，RT 因而读取全远平面深度并输出全可见。现在 RT 事件为 `BeforeRenderingGbuffer - 1`（209），让 URP 在预通道后完成深度复制和运动矢量，再执行追踪。矩形灯上传仍在 Before Opaques。正常 Game 与 Scene View 的屏幕深度探针已确认：修改前接收面全部被判断为远平面，修改后接收面深度有效。
- **切换 Play / Edit 时空 SRV 必须始终有效。** Editor 退出 Play 也会触发 `Application.quitting`；将它当成最终关闭会使清理标志保留，阻止后续 Edit 模式重建空缓冲。RT 关闭时，Console 可报 `_TsukuyomiRTShadowAdditionalLightMap` 未绑定，D3D12 随后跳过 OpenPBR 绘制。Editor 生命周期使用 `EditorApplication.quitting` 处理真正退出。2026-10-25 进一步修复 `ExitingPlayMode` 提前释放回退资源的问题：现在先绑定空 SRV，再释放各相机光追状态，空映射和白色纹理数组保持有效直到真正退出或重新初始化。`EnteredEditMode` 才重建会留下仍可绘制的中间窗口；每相机的命令流也继续显式绑定有效空缓冲。
- **AnyHit 直接读取实例内的双面标志。** 在正常 Game 帧中，保留透明、Alpha Clip 及材质面剔除逻辑，仅移除 `_TsukuyomiRTCasterFlags[InstanceID()]` 的 SRV 读取，软阴影即恢复。修复将 TwoSided 编码为原生实例 ID 的最低位，实例序号放在其余位；AnyHit 使用 `InstanceID() & 1u`，并删除原有标志缓冲及其绑定。这项对照定位到额外 SRV 读取路径，尚不能据此判定是跨相机全局覆盖或其他底层绑定行为。
- **重新创建顶层加速结构，再收集当前实例。** Unity 6000.5.3f1 中，CPU 收集了 17 个投影物时，正常 Scene View 的原生结构曾为 0 个实例，Game 为 17 个。独立 CPU 对照进一步复现：同一个结构第一次 `CullInstances` 为 17，`ClearInstances` 后再次调用为 0；重复清空、逐实例移除及改变 LOD 配置均未恢复，新建结构后连续三次收集均为 17。取消清空虽能显示阴影，却会残留已关闭投影的物体，因此不能作为修复。现在每相机每次收集前重新创建顶层结构，保留原生 LOD、材质筛选和动态几何模式。Unity 的几何 BLAS 生命周期独立于顶层结构；静态网格无需因此重建几何 BLAS，见 [Unity 对加速结构生命周期的说明](https://discussions.unity.com/t/raytracing-acceleration-structure-management/1706479/2)。该兼容处理会增加顶层结构创建与释放开销，下面的旧性能数据未包含这项改动。

前三项修改编译后，已在正常 OpenPBR Game View 中确认矩形软阴影可见；仅将矩形 `ShadowMode` 切到 Off 时阴影消失、材质仍正常显示，切回 RayTraced 后阴影恢复，并保存了 Game 的 On / Off 画面对照。诊断时 Scene View 与 Game View 也曾同时显示软阴影。最终顶层结构更新方式的正常双视图截图、Play / Stop 回归仍需在桌面解锁后完成；这些已有截图不作为最后一项改动的验收证据。

最终实例更新代码已通过 Game 与 Scene View 相机的 CPU 检查：连续收集均为 17 个实例；关闭 Fabric 球的投影后为 16，恢复后为 17；Caster Mask 为 Nothing 时为 0，恢复后为 17。检查不构建或读回 GPU 结果，因此只证明实例集合更新正确，不能替代最终画面验证。

回归时应保持 Scene View 与 Game View 同时打开，在正常帧中检查 Depth Priming Auto / Forced、矩形 Shadow Mode 的 Off / RayTraced 切换，以及进入和退出 Play。除了灯数和阴影槽，还应检查原生加速结构实例数、接收面深度及最终可见度；CPU 收集到投影物并不能证明深度输入和追踪输出有效。临时屏幕探针在验证后移除，不作为材质功能保留。

### RT 生成阶段性能

2026-10-05，在 Unity 6000.5.3f1、RTX 4090、DX12 上完成 98 项资源及渲染检查。覆盖 Forward / Forward+、四类灯混合、预算和排序、Alpha Clip、材质裁面与 Renderer TwoSided、移动及蒙皮投影物、屏幕外投影、多相机、投影物 Layer Mask、运行时关闭和无 Profile 恢复。C# 和 Shader 编译通过，Console 无新增错误。

性能场景使用 Forward+、3840×2160、每像素每灯 8 条射线、64 个静态不透明投影物。场景保持 8 盏矩形灯和 12 盏原生灯，仅改变开启 RT 的灯及灯数；原有阴影路径保持启用。关闭 VSync、帧率上限、后处理和分辨率缩放。各档统一开启 Editor / GPU Profiling，先预热至少 120 帧且 2 秒，再采样至少 5 秒。

FPS 是 **Game View Statistics 的中位数**，时间为对应采样区间的均值，单位毫秒。CPU 列只统计场景收集和实例更新；构建、追踪、降噪三列为独立 GPU 标记。关闭 RT 时对应标记不执行，用“—”表示。

| 配置 | RT 灯数 | Statistics FPS | CPU 场景更新 | GPU 构建 | GPU 追踪 | GPU 降噪 |
|---|---:|---:|---:|---:|---:|---:|
| 关闭 RT | 0 | 381.5 | — | — | — | — |
| 矩形 | 1 | 239.5 | 0.216 | 0.045 | 1.749 | 1.691 |
| 矩形 | 2 | 110.1 | 0.245 | 0.046 | 3.574 | 4.826 |
| 矩形 | 4 | 57.3 | 0.359 | 0.048 | 7.083 | 9.477 |
| 点光 | 1 | 242.9 | 0.216 | 0.045 | 1.774 | 1.646 |
| 点光 | 2 | 110.5 | 0.242 | 0.049 | 3.621 | 4.744 |
| 点光 | 4 | 57.6 | 0.325 | 0.047 | 7.125 | 9.472 |
| 聚光 | 1 | 261.8 | 0.215 | 0.047 | 1.391 | 1.743 |
| 聚光 | 2 | 125.0 | 0.233 | 0.048 | 2.452 | 4.839 |
| 聚光 | 4 | 66.2 | 0.336 | 0.047 | 4.967 | 9.384 |
| 方向光 | 1 | 252.7 | 0.220 | 0.046 | 1.521 | 1.701 |
| 方向光 | 2 | 117.1 | 0.242 | 0.046 | 3.026 | 4.777 |
| 方向光 | 4 | 61.8 | 0.334 | 0.047 | 5.999 | 9.380 |
| 四类混合 | 4 | 60.1 | 0.339 | 0.046 | 6.393 | 9.429 |

这组数据来自上述验证场景；复杂材质、更多或更复杂的投影物、动态几何以及受光材质的计算成本都会改变帧率。四灯混合的 GPU 帧时间约 16.55 ms，三轮全分辨率空间滤波是当前主要开销。当时接收材质使用独立阴影采样输出，不能据此推断 OpenPBR 示例或完整游戏场景的性能。

临时验证场景对象、材质、脚本及原始采样文件已在验证后清理，包内保留功能资源和本文记录。
