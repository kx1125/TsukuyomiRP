# TsukuyomiRP Feature 开发说明

本文档总结 TsukuyomiRP 包内 PCSS Shadow、Contact Shadow、Volume Light 三个功能的开发约定，用于后续在 TsukuyomiRP 中增加新的渲染 Feature。

## 目标原则

- 不修改官方 URP 包。
- 新 Feature 放在 TsukuyomiRP 包内独立目录中，避免和 RenderGraph 封装核心代码混在一起。
- 固定管线能力放在 `Rendering Features` 中配置；可组合、可排序的通用 Pass 放在 `Passes` list 中。
- 资源通过 `TsukuyomiRenderPipelineResources` 预加载，不在运行时使用 `Shader.Find`，也不要求用户手动在 RendererFeature 面板上配置 shader。
- 默认参数来自 `TsukuyomiPipelineProfile`，场景差异通过 Volume Override 覆盖。
- 优先接入 TsukuyomiRP 的 RenderGraph 封装：`RasterPass`、`ComputePass`、`UnsafePass`、`TextureSlot`、`ResourceHub`。

## 推荐目录结构

运行时代码按 Feature 独立建目录：

```text
Packages/tsukuyomi.render-pipelines.universal/
  Runtime/
    Features/
      My Feature/
        TsukuyomiMyFeaturePass.cs
        TsukuyomiMyFeatureVolume.cs
        TsukuyomiMyFeatureResources.cs        可选
        TsukuyomiMyFeatureComponent.cs        可选
  Shaders/
    My Feature/
      MyFeature.shader
      MyFeature.compute
      MyFeature.mat
  ShaderLibrary/
    MyFeatureCommon.hlsl
    MyFeatureSampling.hlsl
```

约定：

- Feature 自己的 C# 文件放在 `Runtime/Features/<Feature Name>/`。
- Feature 自己的 shader、compute、material 放在 `Shaders/<Feature Name>/`。
- 可复用或工具性质的 HLSL 放在 `ShaderLibrary/`，例如采样、深度重建、blur、upsample、shadow helper。
- 不要把具体 Feature 代码放进 `Runtime/Core`、`Runtime/Passes`、`Runtime/Resources`。这些目录只放框架封装。

## 配置来源

每个固定管线 Feature 通常有两层配置。

第一层是全局默认值，写在 `TsukuyomiPipelineProfile`：

```csharp
public bool EnableMyFeature;

[Range(0.0f, 1.0f)]
public float MyFeatureIntensity = 0.5f;
```

第二层是场景覆盖，写 Volume Component：

```csharp
[Serializable]
[VolumeComponentMenu("Tsukuyomi RP/My Feature")]
public sealed class TsukuyomiMyFeatureVolume : VolumeComponent
{
    public BoolParameter enabled = new(false);
    public ClampedFloatParameter intensity = new(0.5f, 0.0f, 1.0f);
}
```

推荐再写一个 resolved settings 结构，把 Profile 默认值和 Volume override 合并：

```csharp
internal readonly struct TsukuyomiMyFeatureResolvedSettings
{
    public readonly bool Enabled;
    public readonly float Intensity;

    public bool IsActive => Enabled && Intensity > 0.0f;

    public static TsukuyomiMyFeatureResolvedSettings From(
        TsukuyomiPipelineProfile profile,
        TsukuyomiMyFeatureVolume volume)
    {
        return new TsukuyomiMyFeatureResolvedSettings(
            Resolve(volume?.enabled, profile.EnableMyFeature),
            Resolve(volume?.intensity, profile.MyFeatureIntensity));
    }

    private static T Resolve<T>(VolumeParameter<T> parameter, T fallback)
    {
        return parameter != null && parameter.overrideState ? parameter.value : fallback;
    }
}
```

这样 pass 中只依赖 resolved settings，不直接散落读取 Profile 和 Volume。

## 资源预加载

默认 shader、compute、material、texture 放进 `TsukuyomiRenderPipelineResources`。

新增字段：

```csharp
[Header("My Feature")]
[SerializeField]
private Shader myFeatureShader;

[SerializeField]
private Material myFeatureMaterial;

public Shader MyFeatureShader => myFeatureShader;
public Material MyFeatureMaterial => myFeatureMaterial;
public bool HasMyFeatureResources => myFeatureShader != null || myFeatureMaterial != null;
```

资源资产维护在：

```text
Packages/tsukuyomi.render-pipelines.universal/Runtime/Data/TsukuyomiRenderPipelineResources.asset
```

运行时获取资源：

```csharp
if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
    return false;

Material material = resources.MyFeatureMaterial;
if (material == null && resources.MyFeatureShader != null)
    material = CoreUtils.CreateEngineMaterial(resources.MyFeatureShader);
```

要求：

- 不新增 `Shader.Find`。
- 不让用户在 `TsukuyomiFeature` 面板上拖 shader。
- 如果必须临时创建 material，pass 需要在 `Dispose()` 中释放自己创建的 material。
- package 内置资源变更后，需要更新 `TsukuyomiRenderPipelineResources.asset` 的引用。

## RenderGraph Pass 选择

优先级建议：

- `RasterPass`：普通 raster 渲染、fullscreen material pass、renderer list。
- `ComputePass`：纯 compute dispatch，读写 texture/buffer 明确。
- `UnsafePass`：需要 `SetRenderTarget`、多阶段 blit、全局关键字、全局纹理，或需要在单个 RenderGraph pass 内维持多个内部绘制阶段。
- `PostPass`：简单后处理，输入 active color，输出新的 active color。

PCSS 和 Volume Light 使用 `UnsafePass`，因为它们需要在一个 RenderGraph pass 内维持和原始实现一致的多阶段绘制结构。

Contact Shadow 使用 `ComputePass`，因为它是 compute 生成资源，denoise 也可以独立 compute pass。

## TextureSlot 用法

`TextureSlot` 用于声明 pass 对内置渲染资源的需求，`TsukuyomiBridgePass.ConfigureInputFromTextureSlots()` 会根据 slot 自动调用 URP 的 `ConfigureInput`。

示例：

```csharp
[Read(BuiltinTexture.CameraDepthTexture)]
public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

[Read(BuiltinTexture.ActiveColor)]
public TextureSlot color = TextureSlot.Read("Color", BuiltinTexture.ActiveColor);
```

在 `Record()` 中解析：

```csharp
TextureHandle cameraDepth = context.GetTexture(depth);
TextureHandle cameraColor = context.GetTexture(color);
```

注意：

- 需要 URP 生成 depth、normal、opaque texture 时，声明对应 `TextureSlot` 即可。
- 不再额外添加一套 `RequiredInputs`，避免同一需求有两套状态源。
- 临时 RenderGraph texture 不需要声明为 `TextureSlot`，直接用 `context.RenderGraph.CreateTexture()` 创建。
- 跨 pass 共享的同帧资源应使用 `FrameResources`/`TsukuyomiFrameResourceRegistry` 的命名资源机制；跨帧持久资源使用 `ResourceHub`。

## 跨 Pass 资源

同一帧跨 pass 共享资源时，以相同 name 作为标记，由 TsukuyomiRP 的 frame resource registry 管理。适合 Contact Shadow 这类先生成、后消费的纹理。

跨帧持久资源使用 `ResourceHub`：

```csharp
RTHandle history = context.ResourceHub.GetOrCreateHistoryTexture("MyFeatureHistory", descriptor);
GraphicsBuffer buffer = context.ResourceHub.GetOrCreateBuffer("MyFeatureBuffer", count, stride);
```

约定：

- 临时中间纹理优先放在当前 RenderGraph pass 内。
- 同帧跨 pass 共享使用稳定 name。
- 跨帧 history/buffer 使用 `ResourceHub`，并依赖 `TsukuyomiFeature.Dispose()` 统一释放。

## 接入 TsukuyomiFeature

固定管线 Feature 需要在 `TsukuyomiFeature` 中注册独立 registry 和 bridge pass。

基本步骤：

1. 添加字段：

```csharp
private PassRegistry _myFeatureRegistry;
private TsukuyomiBridgePass _myFeatureBridgePass;
private TsukuyomiMyFeaturePass _myFeaturePass;
```

2. 如果需要全局 keyword，在 `OnEnable()` 初始化，不要在静态字段初始化中调用 `GlobalKeyword.Create()`：

```csharp
private void OnEnable()
{
    TsukuyomiMyFeaturePass.InitializeKeywords();
}
```

3. 在 `Create()` 中创建 pass、设置 injection point、注册 bridge：

```csharp
_myFeatureRegistry = new PassRegistry();
_myFeaturePass ??= new TsukuyomiMyFeaturePass();
_myFeaturePass.InjectionPoint = InjectionPoint.BeforeOpaque;
_myFeatureRegistry.AddPass(_myFeaturePass);

_myFeatureBridgePass = new TsukuyomiBridgePass(
    _myFeatureRegistry,
    _myFeaturePass.InjectionPoint,
    _resourceHub)
{
    renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 1
};
```

4. 在 `AddRenderPasses()` 中合并 Volume 配置，并按 `Configure()` 返回值决定是否 enqueue：

```csharp
VolumeStack volumeStack = VolumeManager.instance.stack;
TsukuyomiMyFeatureVolume myFeatureVolume = volumeStack?.GetComponent<TsukuyomiMyFeatureVolume>();

if (_myFeaturePass != null && _myFeaturePass.Configure(Profile, myFeatureVolume))
{
    _myFeatureBridgePass.ConfigureInputFromTextureSlots();
    renderer.EnqueuePass(_myFeatureBridgePass);
}
```

5. 在 `Dispose()` 中释放 pass 持有的运行时资源：

```csharp
_myFeaturePass?.Dispose();
_myFeaturePass = null;
_myFeatureBridgePass = null;
```

## 插入队列约定

包内已有功能的插入位置：

| Feature | Bridge Pass Event | Injection Point | 说明 |
| --- | --- | --- | --- |
| Contact Shadow | `AfterRenderingPrePasses + 1` | `BeforeOpaque` | depth prepass 后生成 contact shadow |
| Contact Shadow Denoise | `AfterRenderingPrePasses + 2` | `BeforeOpaque` | 在 contact shadow 后执行 denoise |
| PCSS Screen Space Shadow | deferred: `BeforeRenderingGbuffer`; forward: `AfterRenderingPrePasses + 1/+3` | `BeforeOpaque` | 开启 screen space shadow keyword |
| PCSS Restore Keywords | `BeforeRenderingTransparents` | `BeforePostProcess` | 透明物体前关闭 screen space shadow keyword |
| Volume Light | `BeforeRenderingPostProcessing - 3` | `BeforePostProcess` | 在后处理前合成体积光 |

新增 Feature 时先判断依赖：

- 依赖 depth/normal：通常放在 prepass 后。
- 影响 opaque lighting：通常放在 opaque 前或 gbuffer 前。
- 只合成屏幕效果：通常放在 post process 前。
- 需要恢复 keyword/global state：恢复点应靠近状态不再需要的位置，不要拖到 frame 末尾。

## Pass 编写模板

```csharp
internal sealed class TsukuyomiMyFeaturePass : UnsafePass
{
    [Read(BuiltinTexture.CameraDepthTexture)]
    public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

    private TsukuyomiPipelineProfile _profile;
    private TsukuyomiMyFeatureResolvedSettings _settings;
    private Material _material;
    private bool _ownsMaterial;

    public override string Name => "My Feature";

    public bool Configure(TsukuyomiPipelineProfile profile, TsukuyomiMyFeatureVolume volume)
    {
        _profile = profile;
        if (profile == null)
            return false;

        _settings = TsukuyomiMyFeatureResolvedSettings.From(profile, volume);
        if (!_settings.IsActive)
            return false;

        if (_material != null)
            return true;

        if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
            return false;

        _material = resources.MyFeatureMaterial;
        if (_material == null && resources.MyFeatureShader != null)
        {
            _ownsMaterial = true;
            _material = CoreUtils.CreateEngineMaterial(resources.MyFeatureShader);
        }

        return _material != null;
    }

    public override bool IsActive(in FrameContext frame)
    {
        return base.IsActive(frame) && _profile != null && _settings.IsActive && _material != null;
    }

    public override void Record(in UnsafePassContext context)
    {
        TextureHandle cameraDepth = context.GetTexture(depth);
        if (!cameraDepth.IsValid())
            return;

        context.Builder.UseTexture(cameraDepth, AccessFlags.Read);
        context.Builder.AllowGlobalStateModification(true);

        Material material = _material;
        TsukuyomiMyFeatureResolvedSettings settings = _settings;

        context.SetRenderFunc((data, graphContext) =>
        {
            material.SetFloat("_Intensity", settings.Intensity);
            // draw / dispatch / blit
        });
    }

    public void Dispose()
    {
        if (_ownsMaterial)
            CoreUtils.Destroy(_material);

        _material = null;
        _ownsMaterial = false;
    }
}
```

## Shader 和 HLSL 约定

- Shader 名称优先使用 `Hidden/Tsukuyomi/...`，资源引用必须通过 preload asset。
- Feature 专属 shader 放在 `Shaders/<Feature Name>/`。
- 工具 HLSL 放在 `ShaderLibrary/`，include 使用 package 绝对路径：

```hlsl
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/MyFeatureCommon.hlsl"
```

- 新增工具型 HLSL 时，优先放到 `ShaderLibrary/`，避免散落在具体 Feature shader 目录下。
- 注意 Unity/URP 版本差异：`Blit.hlsl` 可能已经声明 `_BlitTexture_TexelSize`，不要重复声明。
- 如果 shader 依赖官方 URP 内部函数，先确认当前 `Library/PackageCache/com.unity.render-pipelines.universal...` 中对应函数签名。

## Editor 面板

`TsukuyomiPipelineProfileEditor` 将配置分两类：

- `Rendering Features`：固定管线功能，例如 PCSS、Contact Shadow、Volume Light。
- `Passes` list：用户可配置、可组合、可排序的 pass。

新增固定 Feature 时：

- 在 `Rendering Features` 下增加 foldout。
- 参数默认折叠，只露出启用开关。
- 使用 `SerializedProperty` 绘制，避免直接改对象字段。
- UI 风格对齐 Unity Renderer Data / Volume Profile：清晰分组、可折叠、有分割线。

## 验证清单

新增 Feature 后按顺序检查：

1. C# 编译通过。
2. Unity Console 无 error/warning。
3. `TsukuyomiRenderPipelineResources.asset` 已引用 shader/compute/material。
4. 代码中没有新增 `Shader.Find`。
5. Volume Component 能在 Volume Profile 的 Add Override 中找到。
6. Frame Debugger 中 pass 名称清晰，阶段结构符合设计。
7. RenderGraph 模式下资源读写声明正确，没有隐式读写冲突。
8. 关闭 Feature 时不会 enqueue pass，也不会残留 keyword/global texture 状态。
9. 开启 Feature 时 Profile 默认参数生效；Volume override 能覆盖场景参数。
10. 如果创建了临时 material、RTHandle、GraphicsBuffer，`Dispose()` 中能释放。

## 常见问题

### TextureSlot 已声明资源，为什么还需要 Builder.UseTexture？

`TextureSlot` 用来声明 Feature 对 URP 输入的需求，并帮助 bridge pass 调用 `ConfigureInput`。`Builder.UseTexture` 是 RenderGraph 当前 pass 的真实资源读写声明。两者职责不同，都需要。

### 为什么不再添加 RequiredInputs？

`TextureSlot` 已经承担“标记需要哪些内置渲染资源”的职责。再加一套 `RequiredInputs` 会产生重复状态源，容易出现面板、pass、URP input 三处不同步。

### keyword restore 是否一定要单独 pass？

不一定。只有当某个 Feature 开启了会影响后续渲染阶段的全局 keyword，且必须在另一个渲染阶段前恢复时，才需要 restore pass。PCSS 需要在透明物体前关闭 screen space shadow keyword，所以保留 restore pass。

### 一个 Feature 内部多个阶段要不要拆多个 RenderGraph pass？

看渲染路径和资源生命周期。PCSS 的 Penumbra 和 ScreenSpaceShadows 属于同一个屏幕空间阴影流程，放在同一个 `Screen Space Shadows` pass 内分阶段绘制。Volume Light 也在一个 pass 内维持 Downsample/Raymarch/Blur/Upsample/Composite 阶段。Compute 生成和 denoise 这种天然独立、需要跨 pass 消费的流程，可以拆成多个 pass。
