# 实时矩形面光源

## 使用

1. 当前 URP Renderer 上添加 `TsukuyomiFeature`，设置 Pipeline Profile，在 Profile 中启用 **Rect Lights**。默认预算 16，最多 64 盏。
2. 添加 `Rendering > Tsukuyomi Rect Light` 组件。矩形位于局部 XY，向局部 +Z 发光。Width/Height 是世界米数，不受 Transform Scale 影响。
3. 接收物使用 OpenPBR Balanced / High 材质即可获得矩形受光。自定义材质可包含 `Shaders/Rect Light/RectLight.hlsl`，调用 `AccumulateRectLights`，将返回的 diffuse 与 specular 加到直接光结果；输入包括世界坐标、单位法线、单位视线方向、Rendering Layer、Base Color、Metallic、Perceptual Roughness 和镜面多次散射开关。
4. 灯和接收物的 Rendering Layer 必须相交。相机的 Culling Mask 同时过滤灯所在的 GameObject Layer。
5. 可用 `TsukuyomiRP/Rect Lights` Volume 覆盖启用状态和预算。质量始终由每盏灯决定。

预加载的 `TsukuyomiRenderPipelineResources.asset` 已引用 `RectLightResources.asset`。资源不完整时会报告一次警告并禁用贡献。资源资产仅持有运行时使用的四份 LTC/FGD 数据。

支持桌面 Game / Scene View 的 Forward / Forward+。OpenPBR 已接入矩形受光；PBR、SpecularPBR、Deferred、XR 和纹理光源尚未接入。自定义接收材质需提供相应的 Forward、深度、法线和运动矢量 Pass。可选原生 DXR 软阴影的组件设置、投影材质与调用协议见 [原生 DXR 软阴影](RayTracedShadows.md)。

## 三档模型

| 档位 | 漫反射 | 强度与距离 |
|---|---|---|
| Low | UE5.2.1 移动端 Rect-as-Spot，Lambert | 原始颜色强度；光心 N·L；径向八次方和锥角平方衰减 |
| Medium（默认） | UE RectIrradianceLambert 向量辐照度 + SphereHorizonCosWrap | 强度除矩形面积；几何积分产生距离响应 |
| High | HDRP Disney diffuse LTC 矩阵 + diffuse FGD + 多边形积分 | 强度除矩形面积；粗糙度影响 Disney 漫反射 |

Low 的内锥半角 70°、外锥半角 89.001°。径向项为 `saturate(1 - d²/range²)^8`，用连续平方实现；没有反平方项，也没有额外乘一次公共淡出。宽高只影响该灯的矩形高光。

Medium 的边积分使用 UE 的角度权重近似，保留半球边界的包裹余弦近似；不需要 diffuse LUT。

High 使用 HDRP 17.0.4 默认的半球边界近似：`max(0, (|F|² + F.z) / (|F| + 1))`。它包含完整 LTC 查表、矩阵变换和面积积分流程，但仍有拟合误差，不能视作精确 Disney 数值积分。特别是半球边界附近，低能量样本的相对误差可能明显增大。

### 共用高光

通用 `AccumulateRectLights` 接口的三档高光均使用各向同性 GGX、correlated Smith 和 Schlick Fresnel。GGX 矩阵与幅度来自同一个 selfshadow 拟合版本；矩阵和 Disney 的打包方式分别解码。GGX 在变换后的余弦坐标中显式裁剪半球，再积分最多五个顶点。OpenPBR 使用下面说明的独立分层适配。

高光使用单面发光、面积换算和范围窗口 `saturate(1 - (d²/range²)²)²`。Medium/High 漫反射使用同一范围窗口。Low 的 Spot 锥角和八次方径向衰减不影响高光。镜面多次散射补偿使用配套 GGX 幅度表的能量，整个高光只乘一次补偿。

强度是相对艺术控制量，不标为 cd/lm。Low 与另外两档的亮度及距离响应可以不同，不要求远场对齐。感知粗糙度限制在 `[0.02, 1]`，金属度限制在 `[0, 1]`。

## OpenPBR 受光与示例

`TsukuyomiOpenPBRURP.hlsl` 中的 URP 适配器已接入矩形灯和原生灯的 RT 阴影。项目永久示例位于 `Assets/Examples/Tsukuyomi/OpenPBRLighting/OpenPBRLighting.unity`。原生附加灯需要 URP **Additional Lights = Per Pixel** 才会进入逐灯 RT 接收；Per Vertex 保留原有漫反射近似。

灯的 Low / Medium / High 只控制上表中的漫反射模型，材质 Balanced / High 独立选择 OpenPBR 的能量计算。矩形漫反射使用 `diffuseRoughness`，基础 GGX 使用 `specularRoughness`；不会把 OpenPBR 的 EON 修正再乘入 Lambert 或 Disney。切换灯质量不会改变 base、coat、fuzz 高光或 RT 采样模型。

分层使用 OpenPBR 已准备的 IOR 和金属度能量，先从基础漫反射中扣除基础反射能量，再按 coat 覆盖和吸收缩放基础层、加入独立法线下的 coat 反射，最后应用 fuzz 层。IOR 为 1 的非金属基础层或 coat 不产生 Fresnel 反射。当前不透明 OpenPBR 子集没有独立的 `baseWeight` / `specularWeight` 材质字段；`coatWeight` 和 `fuzzWeight` 保留现有含义。

矩形高光的近似范围：

- Base 和 coat 复用现有各向同性 GGX LTC 形状。Balanced 使用其补偿后的方向能量；High 将 IOR 相关的单次散射能量与宽余弦 LTC 的多次散射能量分别积分。
- 各向异性在切线空间拉伸方向后查询各向同性 LTC；它不是单独拟合的各向异性 LTC 表。镜像 UV 的切线符号不会翻转积分绕序。
- Fuzz 直接积分 OpenPBR 已有的 fuzz LTC。Coat 使用自身法线、粗糙度和 IOR；High 的吸收按光心方向近似。
- 共享 GGX 形状来自 correlated Smith / Schlick 拟合，而 OpenPBR 点光模型使用 separable Smith，High 使用精确介质 Fresnel。这里保留分层参数与能量，仍有形状拟合、各向异性拉伸及多次散射角分布的近似误差。

面积积分已包含余弦与 `1/PI`，不能再乘原生点光适配中的 `PI * NdotL`。有效矩形 RT 可见度在 diffuse 与全部 specular 层合成后各应用一次。`_RECEIVE_SHADOWS_OFF` 禁用 RT 接收；透明接收表面保留原阴影路径。

原生灯有有效 RT 结果时，适配器替换其实时阴影来源，保留 ShadowMask 合成、Cookie 和 Rendering Layer；无有效结果时回退到 URP。没有对应 raster shadow keyword 时，RT 使用独立发布的距离淡出参数，仍按 URP Asset 的 Shadow Distance 和 Cascade Border 合成烘焙阴影，不读取其他相机残留的阴影参数。Shadow Distance 为 0 时，显式开启的 RT 保持有效且不做距离淡出。

## 实现结构与生命周期

- `TsukuyomiRectLight` 注册当前启用的灯；每盏灯的 GPU 数据为 96 字节，包含完整 32 位 Rendering Layer 和质量枚举。
- `TsukuyomiRectLightPass` 通过现有 `TsukuyomiBridgePass` 在 BeforeRenderingOpaques 上传。每相机进行范围包围盒视锥剔除，按光心距相机距离排序，以 EntityId 打破并列；超预算警告一次。
- `ResourceHub.ForCamera` 持有该相机独立的 Structured Buffer。RenderGraph 节点保存独立的数据快照；命令缓冲在执行时上传，避免多个相机覆盖待执行的数据。
- LUT 只上传一次，线性空间、双线性过滤、Clamp、无 mip。GGX 材质查表位于灯循环外；相机列表有 High 灯时才准备 Disney 数据。
- 当投影半尺寸小于 0.001 弧度时，使用多边形固体角的一阶矩极限，避免 FP32 边向量相减导致远处极小矩形出现亮斑。GGX/Disney 仍使用各自 LTC 变换；Medium 保留 UE 角度权重的极限值。该分支省略项为角尺寸的二阶量。
- 无灯、关闭、无 Profile、缺资源时清零贡献并释放该相机灯缓冲；销毁相机由 ResourceHub 回收。共享 LUT 使用引用计数，资源重载时重建。
- 相机开始/结束时重置全局计数，避免其他 Renderer 的相机沿用灯列表。D3D12 在零灯时仍需要合法 SRV，因此保留一个 96 字节空缓冲。Edit / Play 状态切换期间保持其有效，包括 `ExitingPlayMode` 到 `EnteredEditMode` 之间；真正退出、脚本域重载或子系统重新初始化时才清理或重建。

## LUT 来源与构建

资源共 104 KiB：GGX 矩阵 32 KiB、GGX 幅度 32 KiB、Disney 矩阵 32 KiB、Disney FGD 8 KiB。固定版本、映射和 SHA-256 位于 `Runtime/Features/Rect Light/LUT/manifest.json`。

- Unity Graphics / HDRP 17.0.4：`feb4de2d9a93a4ae10d287d3a6d7003d08ea3e53`。
- selfshadow/ltc_code：`31e5e96b54f98f33098f8503003119ba2231a1c6`。
- Low 行为核对本机 UE5.2.1 的 RectLightComponent、LightData 和 DynamicLightingCommon；Medium 对应参考文档的 UE 矩形 Lambert 算法，并核对本机 RectLight/CapsuleLight 中相同公式。这里没有把本机版本称为 UE5.8 实测。

在包目录执行（Python 3、NumPy）：

```text
python Tools~/RectLight/fetch_sources.py <source-directory>
python Tools~/RectLight/build_luts.py --reference <source-directory>
```

FGD 使用 HDRP DisneyDiffuseNoPI 模型及 `R + 0.5` 解码，采用解析方位角矩和 64 点 Gauss-Legendre 积分重新生成；不是 HDRP 4096 样本 Monte Carlo 纹理的逐字节拷贝。Unity 与 selfshadow 许可及论文引用位于 `Third Party Notices~/RectLight`，发布时保留。
