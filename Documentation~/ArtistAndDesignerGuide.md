# TsukuyomiRP 美术与策划使用指南

本指南面向场景美术、角色美术、技术美术和关卡/视觉策划，说明如何在 Unity 编辑器中启用、调节和验收 TsukuyomiRP 的画面功能。本文对应包版本 **0.1.1**（Unity 6000.5、URP 17.5）。

## 功能概览

| 功能 | 主要用途 | 配置位置 | 主要成本 |
| --- | --- | --- | --- |
| Planar Reflection | 水面、镜面地板等平面实时反射 | Pipeline Profile + 反射面组件 | 额外渲染一次指定图层 |
| PCSS Screen Space Shadows | 主方向光的软阴影、接触处硬而远处软 | Pipeline Profile / Volume | 阴影采样数与屏幕分辨率 |
| Per Object Shadows | 角色等重点对象的独立高精度阴影 | Pipeline Profile / Volume + 组件 | 每个阴影簇占用图集区域 |
| Contact Shadows | 补足小物体和接触处的屏幕空间阴影 | Pipeline Profile / Volume | 光线步进与可选降噪 |
| GTAO | 增强缝隙、墙角和物体接触处的环境遮蔽 | Pipeline Profile / Volume | 方向数、步数和分辨率 |
| Volume Light | 主光与局部光的雾中光束 | Pipeline Profile / Volume + 局部光组件 | 光线步进、模糊与参与光源数 |
| SSS Skin | 皮肤的屏幕空间次表面散射 | Pipeline Profile / Volume + 专用材质 | 采样次数、迭代和作用图层 |
| Custom Bloom | 可控的高亮泛光 | Pipeline Profile / Volume | 多级模糊与合成 |
| Tonemapping | 将 HDR 亮度映射到显示范围并塑造对比度 | Pipeline Profile / Volume | 较低 |
| FSR3 Upscaler | 低分辨率渲染后进行时域超分 | Project Settings + URP Upscaling Filter | 时域历史、运动矢量与计算着色器 |
| Tsukuyomi PBR Shader | 通用 PBR、RMOE 打包贴图及 Tsukuyomi 光照参数 | Material Inspector | 取决于材质功能 |

> **配置规则：** Pipeline Profile 是项目/画质档的默认值；Volume Override 只在字段左侧勾选时覆盖默认值。若 Profile 中的功能总开关关闭，Volume 不能单独把它打开。

## 首次接入

### 1. 检查默认资源

打开 **Edit > Project Settings > Tsukuyomi RP**。在 **Default Render Resources** 中确认显示 `Preloaded resource`。正常情况下包会自动注册默认资源；若丢失，点击 **Use Package Default**。

资源资产属于管线内部依赖。美术和策划通常不需要修改其中的 Shader、Compute Shader 或 Material 引用。

### 2. 创建 Pipeline Profile

在 Project 窗口中右键，选择 **Create > TsukuyomiRpP > Pipeline Profile**。建议按画质档建立多个 Profile，例如：

- `TsukuyomiProfile_High`：完整效果；
- `TsukuyomiProfile_Medium`：降低采样、半分辨率 GTAO；
- `TsukuyomiProfile_Low`：关闭平面反射、体积光或 SSS 等高成本功能。

### 3. 添加 Renderer Feature

找到当前 URP Renderer Data，在 Inspector 的 **Renderer Features** 中添加 `TsukuyomiFeature`，然后把上一步创建的 Profile 拖到 **Profile** 字段。

没有 Profile 时，TsukuyomiRP 不执行固定渲染功能。更换 Renderer Data 或 Quality Level 时，也要确认实际使用的 Renderer 上存在该 Feature。

### 4. 建立场景 Volume

创建全局 Volume，并在 Volume Profile 的 **Add Override > TsukuyomiRP** 下添加需要的功能。全局基准值可以只放在 Pipeline Profile；只有需要按场景、区域或时间混合的参数才放进 Volume。

相机需要启用 **Post Processing** 才能看到 Bloom 和 Tonemapping。Volume 的 Layer、相机的 Volume Mask、Is Global、Blend Distance、Weight 与 Priority 仍遵循 URP 标准规则。

## 推荐工作流

1. 由技术美术在 Pipeline Profile 中制定画质档默认值和预算。
2. 场景美术用全局 Volume 建立关卡基调，用局部 Volume 做室内外、天气和剧情区域变化。
3. 角色/道具美术只给确实需要的对象添加平面反射、逐物体阴影或 SSS 所需组件和材质。
4. 策划通过 Volume 的 Weight、Priority 及动画系统控制效果强度，不直接修改包内资源。
5. 在目标分辨率和目标硬件上用 Game View、Profiler 和 Frame Debugger 验收；Scene View 只用于预览。

## Planar Reflection（平面反射）

适合平静水面、镜面地板和大面积光滑平面。当前 V1 **同一时刻只使用一个活动反射面**；存在多个时选择 `Priority` 最大者。

### 设置

1. 在 Pipeline Profile 中打开 **Planar Reflection**。
2. 设置 **Render Texture Scale**：`0.5` 是常用起点，`1.0` 更清晰但成本显著增加。
3. 用 **Layer Mask** 排除无需出现在反射中的特效、UI、隐藏辅助物和反射面自身。
4. 反射面 Renderer 使用 Shader **TsukuyomiRP/Lit/PBR**。
5. 在同一个 GameObject 上添加 `Tsukuyomi Planar Reflection Plane`。

### 组件字段

| 字段 | 说明 |
| --- | --- |
| Enabled In Scene | 临时参与/退出反射面竞争 |
| Auto Detect Plane | 按网格包围盒最薄轴推断平面法线 |
| Plane Transform | 关闭自动检测时，以该 Transform 的局部 Y 轴作为法线；也可单独控制平面位置 |
| Plane Offset | 沿法线移动数学反射平面 |
| Priority | 多个活动面中数值最大者生效 |
| Clip Plane Offset | 避免反射相机裁剪面附近穿帮；过大可能切掉贴近表面的物体 |

若反射上下颠倒或方向不对，关闭 Auto Detect Plane，建立一个局部 Y 轴朝向期望法线的空物体并赋给 Plane Transform。

## PCSS Screen Space Shadows

PCSS 让主方向光阴影随遮挡物与受光面距离产生软硬变化。它依赖主光阴影和屏幕空间数据，不会替代灯光本身的 Shadow 设置。

| 参数 | 视觉影响与调节建议 |
| --- | --- |
| Find Blocker Sample Count | 寻找遮挡物的采样数；噪点明显时提高 |
| PCF Sample Count | 最终滤波采样数；阴影边缘颗粒明显时提高 |
| Angular Diameter | 光源角直径；越大整体半影越宽 |
| Blocker Search Angular Diameter | 遮挡搜索范围；过大会串入无关遮挡 |
| Min Filter Max Angular Diameter | 限制近距离最小滤波的角范围 |
| Max Penumbra Size | 最大半影宽度，防止远距离阴影过糊 |
| Max Sampling Distance | 最大屏幕空间采样距离 |
| Min Filter Size Texels | 最小滤波半径，适合抑制锯齿 |
| Penumbra Mask Scale | 半影遮罩降采样倍率；数值越大通常越省但越容易丢细节 |

先固定灯光和相机，调 Angular Diameter 得到目标软硬关系，再用两组 Sample Count 消除可见噪点。不要一开始同时提高所有采样参数。

## Per Object Shadows（逐物体阴影）

用于主角、Boss、载具等重点对象，让它们获得独立阴影图分配。需要方向主光；建议在 URP Asset 中开启 **Use Rendering Layers**，否则对象可能仍进入主光阴影图，造成重复或预算浪费。

### 设置

1. 在 Profile 打开 **Per Object Shadow**，选择专用 **Rendering Layer**。
2. 根据平台选择 **Depth Bits** 和 **Tile Resolution**。先用 Depth16；只有精度问题明确时再提高。
3. 给角色根节点添加 `Tsukuyomi Per Object Shadow Renderer`。
4. 在 **Cluster Data** 中建立阴影簇：`Render Object` 是代表对象，`Renderers` 收集同一簇的身体、服装和配件 Renderer。
5. 选中对象观察绿色包围盒 Gizmo；若自动包围盒过大，用手动 Bounds 缩紧。

`Rendering Layer Mask` 应与 Profile 保持一致；运行时 Profile 会把活动组件统一到 Profile 的掩码。`Shadow Length Offset` 用于扩展沿光照方向的包围范围，阴影被截断时提高，过大则降低图集利用率。

尽量把一个角色合并为少量合理阴影簇。大量小簇会增加管理与绘制成本；过大的簇又会降低有效阴影分辨率。

## Contact Shadows（接触阴影）

用于补足脚底、桌脚、小道具接触处以及传统阴影图难以保留的微小阴影。它是屏幕空间效果：屏幕外遮挡物、被其他物体挡住的深度信息无法贡献阴影。

| 参数 | 说明 |
| --- | --- |
| Length | 光线追踪长度；越长覆盖越多，也更容易出现穿帮 |
| Distance Scale Factor | 随观察距离缩放追踪长度 |
| Min / Max Distance | 生效的相机距离范围 |
| Fade In / Fade Distance | 近端淡入及远端淡出范围 |
| Ray Bias | 减少自阴影；过大使阴影与物体分离 |
| Thickness Scale | 遮挡厚度容忍；薄物体漏光时可提高 |
| Sample Count | 追踪步数；提高质量也提高成本 |
| Denoiser | `Spatial` 可降低噪点，但会增加计算并可能模糊细节 |
| Filter Size | Spatial 降噪核大小 |

推荐只把 Contact Shadows 用作短距离细节层，不要试图用很长的 Length 替代主光阴影。

## GTAO

GTAO 增强环境光遮蔽，可用于建筑转角、道具接触和角色褶皱。它不是投影阴影，强度过高会产生“脏边”和过度描线。

| 参数 | 说明 |
| --- | --- |
| Down Sample | 半分辨率计算；通常建议开启 |
| Intensity | AO 总强度 |
| Direct Lighting Strength | AO 对直接光的影响；写实场景宜保守 |
| Radius | 世界/观察尺度上的遮蔽范围 |
| Thickness | 厚度假设；影响薄物体附近的漏光和过遮蔽 |
| Spatial Bilateral Aggressiveness | 空间双边滤波强度 |
| Blur Sharpness | 模糊保边程度 |
| Step Count | 每方向步数，主要影响稳定性和细节 |
| Maximum Radius In Pixels | 屏幕上最大搜索半径 |
| Direction Count | 采样方向数，成本影响明显 |

调节顺序建议为 Radius → Intensity → Direct Lighting Strength，再提高 Step Count 或 Direction Count 解决质量问题。

## Volume Light（体积光）

该功能模拟均匀/高度限制雾介质中的主光和局部光散射。

### 全局介质与主光

| 参数 | 说明 |
| --- | --- |
| Distance | 相机前方参与体积光的最大距离 |
| Base / Maximum Height | 雾层的世界高度范围 |
| Enable Ground / Ground Height | 启用地面边界，限制地面以下参与介质 |
| Density | 介质密度；过高会快速洗白画面 |
| Attenuation Distance | 介质随距离衰减尺度 |
| Probe Volume Contribution | 使用 Probe Volume 的环境贡献及其权重 |
| Main Light Contribution | 主方向光是否产生体积散射 |
| Anisotropy | `>0` 强化朝光方向光束，`<0` 强化背向散射 |
| Scattering / Tint | 主光散射强度和颜色 |
| Additional Lights Contribution | 是否计算已注册的局部光 |
| Max Steps | 光线步进上限，主要质量/性能旋钮 |
| Blur Iterations | 模糊次数，降低噪点但损失细节 |
| Transmittance Threshold | 透射率很低时提前停止步进 |

### 局部光

给 Point 或 Spot Light 添加 `Tsukuyomi Volumetric Additional Light`：

- **Anisotropy**：局部光散射方向性；
- **Scattering**：局部光在雾中的强度；
- **Radius**：光源体积半径/柔化尺度。

只有添加此组件且 Profile/Volume 打开 Additional Lights Contribution 的局部光才参与。控制参与光数量通常比单纯降低画质更有效。

## SSS Skin

SSS Skin 对指定 Layer 中使用 Tsukuyomi 皮肤 Shader 的对象做屏幕空间扩散。先把角色皮肤 Renderer 放入独立 Layer，再把该 Layer 填入 Profile 的 **Layer Mask**，避免处理服装和场景。

包内提供 `TsukuyomiRP/SSS Skin/PBR` 与 `TsukuyomiRP/SSS Skin/Standard` Shader。材质、Profile 总开关和 Layer Mask 三者缺一不可。

| 参数 | 说明 |
| --- | --- |
| Quality | High / Medium / Low 质量档 |
| Scattering Radius | 皮下扩散半径，需结合角色真实尺度 |
| Scattering Iterations | 扩散迭代次数 |
| Shader Iterations | 单次扩散采样数 |
| Depth Test | 防止跨深度边界串色；穿帮时减小容忍范围 |
| Normal Test | 防止跨法线边缘串色 |
| Max Distance | 超出相机距离后停止 SSS |
| SSS Color | 扩散颜色，皮肤通常是偏暖而非纯红 |
| Randomized Rotation | 随机旋转采样核，减少规则纹理 |
| Dither Scale / Intensity | 抖动尺度与强度 |
| Noise Texture | 自定义抖动噪声纹理 |

先用较低半径和暖色建立自然效果，再增加采样。耳朵等薄区域的透光并不等同于全屏提高 SSS 强度，应优先从材质与灯光共同处理。

## Custom Bloom

| 参数 | 说明 |
| --- | --- |
| Threshold | 开始产生 Bloom 的亮度阈值 |
| Intensity | 合成强度 |
| Lum Range Scale | 参与 Bloom 的亮度范围尺度 |
| Pre Filter Scale | 预过滤尺度，影响亮区选择与稳定性 |
| Blur Composite Weight | 四级模糊结果的合成权重（X 细节最多，W 范围最宽） |
| Tint | Bloom 染色；建议接近白色并保持克制 |

Bloom 需要 HDR 高亮输入。若所有材质亮度都被限制在低动态范围，提高 Intensity 也难以得到自然光晕。

## Tonemapping

模式包括 **None、Neutral、ACES、ACES Simple、Gran Turismo**。Neutral 和 ACES 适合快速建立可靠基准；Gran Turismo 提供更多曲线参数：

- **Max Brightness**：输出肩部的目标最大亮度；
- **Contrast**：整体曲线对比度；
- **Linear Section Start / Length**：中间线性段起点与长度；
- **Black Pow / Black Min**：暗部曲线形状与最低黑位。

项目若同时使用 URP 自带 Tonemapping，应只保留一套，以免重复映射造成高光压缩和颜色偏差。

## FSR3 Upscaler

FSR3 是项目级时域超分功能，不在 Pipeline Profile 中配置。

1. 打开 **Project Settings > Tsukuyomi RP > FSR3 Settings**，勾选 **Enabled**。
2. 选择 **Quality Mode**；Quality 是推荐起点，Performance 更快但细节与稳定性下降。
3. 按需启用 **Perform Sharpen Pass** 并调节 **Sharpness**。
4. 在 URP Asset 的 Upscaling Filter 中选择 **Tsukuyomi FSR3**（具体字段名称随 Unity 版本可能略有变化）。

**Velocity Factor** 用于校准运动矢量强度，通常保持 `1`；拖影或运动补偿明显不正确时再由技术美术校准。**Enable Auto Exposure** 让 FSR3 使用自动曝光处理亮度变化。FSR3 是时域方案，依赖深度和运动矢量；快速运动、透明粒子、细线和镜面高光是重点验收对象。不要同时给同一相机使用 TAA 与 FSR3；实现会记录冲突提示。当前实现不支持 XR。**Enable Debug View** 仅用于开发检查，发布画面应关闭。

## Tsukuyomi PBR 材质

创建材质并选择 **TsukuyomiRP/Lit/PBR**。核心贴图工作流：

- **Base Map / Base Color**：基础色与透明度；
- **RMOE Map**：`R = Roughness`、`G = Metallic`、`B = Ambient Occlusion`、`A = Emission Mask`；
- **Roughness / Metallic / Occlusion Strength**：对打包贴图结果进行整体缩放；
- **Normal Map / Scale**：切线空间法线；
- **Emission**：发光颜色乘以 RMOE 的 Alpha 通道。

**Tsukuyomi Lighting** 中：Micro Shadow Opacity 控制微阴影；Rough Diffuse Strength 调整粗糙漫反射；Indirect Diffuse/Specular Intensity 控制间接光；Indirect Specular FGD Strength 调整间接高光能量；Horizon Occlusion Power 抑制法线地平线附近的错误反射。

透明材质支持 Alpha、Premultiply、Additive、Multiply。只有 `TsukuyomiRP/Lit/PBR` 材质会由平面反射组件自动启用对应关键字。

## 性能分级建议

以下是起点而非硬性标准，应以目标场景的 GPU 数据为准。

| 档位 | 建议 |
| --- | --- |
| High | 平面反射 0.5–1.0；PCSS 16/32；GTAO 半分辨率 6 步/2 方向；体积光 64–128 步；重点角色 High SSS |
| Medium | 平面反射 0.5 或按场景关闭；降低 PCSS 采样；GTAO 保持半分辨率；体积光 32–64 步；Medium/Low SSS |
| Low | 关闭平面反射、局部体积光和 SSS；Contact Shadow 短距离；低采样 PCSS/GTAO，或按项目取舍关闭其一 |

常见成本排序通常为：额外场景渲染（平面反射）和高步数体积光最重；随后是高采样阴影、SSS、GTAO；Bloom 与 Tonemapping 相对较轻。实际排序会随分辨率、参与对象和灯光数量变化。

## 验收清单与排错

### 效果完全不出现

- Renderer Data 是否添加 TsukuyomiFeature，并指定了 Profile；
- Profile 总开关是否开启；
- Volume 对应字段左侧是否勾选，Volume Layer 是否在相机 Mask 中；
- Project Settings 是否存在预加载的默认资源；
- 后处理效果还需确认相机 Post Processing；
- Scene View 与 Game Camera 是否使用相同 Volume 和 Renderer。

### 画面闪烁、噪点或拖影

- PCSS/Contact Shadow/GTAO：先缩短搜索范围，再提高必要的采样数；
- FSR3：检查运动矢量、透明物和 TAA 冲突；
- SSS：降低 Dither Intensity，检查 Depth/Normal Test；
- 体积光：增加 Blur Iterations 或 Max Steps，并减少极端 Anisotropy。

### 边界穿帮

- 平面反射：检查平面法线、Plane Offset 与 Clip Plane Offset；
- Contact Shadow：降低 Length，调 Ray Bias 与 Thickness；
- SSS：收紧 Layer Mask、Depth Test 和 Normal Test；
- Per Object Shadow：查看绿色 Bounds 是否完整包住对象及其动画范围。

### 发布前

- 在所有 Quality Level/Renderer Data 上检查 Feature 与 Profile；
- 用目标显示分辨率和动态分辨率检查屏幕空间效果；
- 检查相机切换、Timeline、Volume 混合和加载新场景时是否突变；
- 关闭 FSR3 Debug View；
- 检查 Console 无资源缺失、TAA 冲突或 Rendering Layers 警告。
