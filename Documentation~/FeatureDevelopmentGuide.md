# Tsukuyomi Profile Pass 开发指南

## 配置和使用

`TsukuyomiFeature` 只负责配置同步、相机准备、运行时生命周期和计划入队。所有效果来自 `TsukuyomiPipelineProfile.Passes`；Profile 只保存节点配置和布局，没有效果专用字段或固定装配路径。

新建 Profile 默认没有效果。在 Inspector 打开 Graph Editor，双击渲染阶段，在菜单中添加Pass，然后点击 **Save & Apply**。Profile Inspector 只显示已添加节点的 Enabled 和参数。节点按阶段内纵向位置排序；复制会生成新 ID。自动生产者和恢复 Pass 可在节点参数下的 **Compiled passes and dependencies** 中查看。

Graph Editor 编辑的是草稿。修改 Inspector 后，已有草稿必须 Reload，不能覆盖外部新配置。Undo/Redo、复制和重新打开通过稳定 NodeId 对应参数。缺失Pass类型时禁止应用和迁移，先恢复程序集，避免丢失序列化数据。

选中节点后可直接编辑参数及 Enabled；编辑只改变草稿，点击 **Save & Apply** 才写入 Profile 并生效。保存会提交当前输入框尚未提交的值，Profile Inspector 随后显示新配置。展开参数面板不会标记配置修改。若 Profile Inspector 已有新修改，保存会提示 Reload 并保留双方数据，不会用旧草稿覆盖它。

**Enabled 是装配总开关。** 未添加或关闭节点不会创建其运行时，也不会参与依赖请求。Volume 和静态相机 API 只能控制已启用节点。参数顺序为节点默认值、有效 Volume override、显式相机覆盖。运行时修改节点参数后调用 `profile.NotifyChanged()`；总开关也可用 `profile.SetNodeEnabled(id, enabled)`。

## 最小 Pass

Pass放在自己的目录或独立程序集，引用 `Tsukuyomi.Rendering`、Core RP 和 URP Runtime。不要向 Feature、Profile、Bridge、中央资源类或统一 Inspector 添加具体效果分支。

```csharp
using System;
using Tsukuyomi.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

[Serializable, InjectionPoint(InjectionPoint.BeforePostProcess)]
public sealed class MyPass : PostPass
{
    public Shader Shader;
    [Range(0, 1)] public float Intensity = 1;
    [NonSerialized] private Material _material;
    [NonSerialized] private float _intensity;
    public override string Name => "My Pass";
    public MyPass() => InjectionPoint = InjectionPoint.BeforePostProcess;

    public override void Initialize()
    {
        if (Shader) _material = CoreUtils.CreateEngineMaterial(Shader);
    }
    public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
    {
        _intensity = Mathf.Clamp01(Intensity);
        // 在这里继续解析 context.Volumes 和显式相机覆盖。
    }
    public override bool IsActive(in FrameContext frame)
        => Enabled && _material && _intensity > 0;

    private sealed class Data
    {
        public Material Material;
        public TextureHandle Source;
        public float Intensity;
    }
    public override void Render(in PostPassContext context, TextureHandle source, TextureHandle destination)
    {
        var data = context.GetOrCreateData<Data>();
        data.Material = _material;
        data.Source = source;
        data.Intensity = _intensity;
        context.SetRenderFunc(data, static (snapshot, ctx) =>
        {
            snapshot.Material.SetFloat("_Intensity", snapshot.Intensity);
            Blitter.BlitTexture(ctx.cmd, snapshot.Source, new Vector4(1, 1, 0, 0), snapshot.Material, 0);
        });
    }
    public override void Deactivate(Camera camera) => _intensity = 0;
    public override void Dispose()
    {
        CoreUtils.Destroy(_material);
        _material = null;
    }
}
```

普通效果只需这一个类和自己的 Shader。`RasterPass`、`ComputePass`、`UnsafePass`、`PostPass`、`GraphFeaturePass` 都继承同一个 `PrepareRecording` 接口。基类默认创建当前 Pass 的运行时，并按节点阶段加入计划，不需要额外 Definition 或 Runtime 文件。

Profile 中的实例是配置模板；PassSession 克隆 Profile 后才创建计划和调用生命周期。可序列化字段只保存参数与资源资产引用，材质、History、活动相机和解析结果使用非序列化运行时字段，不能写回 Profile。需要兼容既有序列化字段或拆分复杂配置时，仍可选择 `RenderPassDefinition.CreateRuntime()`；配置和实际 Pass 可以放在同一个文件里。

一个用户节点也可覆盖 `Build` 声明多个 Pass。`Build` 只声明结构，不创建 GPU 资源、不修改全局状态；编辑器也会调用它进行校验。`Initialize` 才分配资源。

原生 `ScriptableRenderPass` 也在 `Build` 中创建，不要放进 Profile Pass 的字段初始化器：其构造函数可能调用 `ShaderTagId` 等 Unity API，而 Profile 的序列化过程不能调用这些 API。此类运行时字段应标记 `NonSerialized`。

运行时无逐帧类型扫描或 JSON 克隆；结构只在 Profile 版本、开关、节点或阶段变化时重建。Editor 用 TypeCache 自动发现公开、非抽象、可序列化且有默认构造函数的 `RenderPassBase` 派生类；允许阶段由 InjectionPoint 属性声明。默认单例，只有资源按节点隔离的Pass才覆盖 `AllowMultiple => true`。

独立程序集只需引用运行时接口，自行提供 Pass、Shader 和必要的辅助服务。`PassLinkerProcessor` 自动生成保留定义及直接配置 Pass 的 link.xml，无需维护中央类型名单。

## 计划和依赖

- `plan.Add(pass, event, active, resolveStage)` 将 Raster、Compute、Unsafe、Post 或 GraphFeature Pass 接入通用 Bridge。
- `plan.AddNative(pass, event, active, resolveStage)` 接入原生 URP ScriptableRenderPass，仍遵守依赖与生命周期。
- `plan.Provides(handle, key)` 声明能力输出；`plan.Requires(handle, key, optional)` 声明输入；`plan.After(consumer, producer, optional)` 声明直接顺序。
- `plan.Service(key, factory)` 获取共享辅助运行时；Key 必须包含格式、布局、阶段等兼容条件。只在存在已启用消费者时创建，一个兼容 Key 只有一个实例。
- `plan.PrepareAfter<T>()` 声明 CPU 配置解析顺序，可使用能力接口。先调用效果的 `PrepareRecording`，再准备共享服务，然后收集所有 Pass 的资源需求，最后调用 `ResolveResources`。活动判断和录制使用最终解析结果。使用独立 Runtime 时，其 `Prepare` 先于它注册的子 Pass 的 `PrepareRecording`。
- `context.Find<T>()` 或 `context.Collect<T>(list)` 获取能力接口。用接口连接Pass，不在框架中匹配具体效果类型。

资源需求同样使用开放接口：Pass 在 `CollectResourceRequirements` 中调用 `requirements.Require(new MyRequest(...))`，辅助服务在 `ResolveResources` 中通过 `context.Requirements.GetRequests<MyRequest>()` 读取请求，并用 `SetStatus` 标记结果。`MyRequest` 是效果自己定义的结构体，合并兼容请求和支持条件由生产者处理；未解析请求会阻止对应消费者执行。框架不需要新增资源种类字段、分支或专用收集方法。

调度先满足阶段边界和依赖，再按图中顺序稳定排序。必需生产者不存在、循环依赖、静态阶段冲突、非法阶段、重复 ID 和单例冲突均阻止应用。动态资源/平台不足则跳过受影响分支。可选依赖不因生产者关闭而关闭消费者；存在且参与执行时仍满足顺序约束。

动态阶段按每次相机调用解析；阶段变化时复用已编译依赖重新排序，同阶段使用 Profile 中的顺序，不能沿用上一相机的入队顺序。阶段不变时复用排序结果，不重建计划。

恢复全局 GPU 状态必须声明为有序 RenderGraph Pass，例如主 Pass 后的 keyword 恢复。CPU 外部对象修改要在 `Deactivate` 恢复。`Deactivate` 在相机完成后也被调用，应幂等，不能释放仍被已录制工作引用的 GPU 资源。

核心框架不识别 Bloom、PCSS 或 Scene Scan 类型。共享屏幕阴影、Scene Scan 合成和后处理融合都属于效果模块。阴影之间通过 `IContactShadowProvider`、`IPerObjectShadowProvider` 和 `IPcssShadowSettingsProvider` 连接。关闭 PCSS 不会关闭 Contact Shadow 所需的基础阴影合成，也不会使 PCSS 自动启用。

第三方Pass需要内置 Depth Pyramid 时，在 Build 中调用 `DepthPyramidDependency.Require(plan, consumer)`，并在消费 Pass 的 `CollectResourceRequirements` 中声明 `RequireDepthPyramid(request)`。服务合并兼容请求，`ResolveResources` 在所有效果及服务参数解析之后处理这些请求；无需修改框架注册表。

## RenderGraph 录制

优先使用现有 `RasterPass`、`ComputePass`、`UnsafePass`、`PostPass`、`GraphFeaturePass` 封装。`GraphFeaturePass` 适合一个效果中的多节点算法，多个跨阶段工作则用 Build 声明多个计划项。

通过 TextureSlot/BufferSlot 声明资源读写；使用 ContextItem 或Pass自己的资源对象共享当次相机调用的数据，禁止缓存 TextureHandle 到下一帧。需要颜色采样时声明 ActiveColor 读取和 intermediate target，避免采样 backbuffer。

PostPass 使用 `context.Blit(material)` 或 `context.SetRenderFunc`，不要直接向 Builder 设置回调绕过输出有效性标记。提前返回时不能把尚未写入的目标当作后续颜色输入。

RenderGraph 延后执行。回调引用的数据必须是该录制节点拥有的快照；使用 `GetOrCreateData<T>()` 取池化数据并覆盖所有参与回调的字段，使用 static 回调避免捕获运行时的可变字段。不得让不同节点共用会在录制过程中修改绑定的材质实例。

## 资源和生命周期

`Initialize` 每个运行时实例调用一次。`PrepareRecording` 每次相机渲染调用一次，包括同帧重复渲染，并且先于 `IsActive` 和资源需求收集；不要按帧号跳过它。这里解析参数、准备材质和 CPU 状态，不读取尚未生成的 RenderGraph 纹理句柄。原有 `Setup` 仍在录制时调用，不承担准备阶段的参数解析。

相机类型和 Camera Stack 限制也应在准备阶段落实，避免不支持的相机仍请求资源或执行恢复 Pass。内置屏幕效果及 Scene Scan 跳过 Preview；Scene View 保留支持的效果，第一人称和 SSGI 保持各自的 Game/Base 相机限制。Neural Rendering 的 FinalCameraInStackOnly 在准备阶段生效。中间相机替换活动颜色时，Bridge 会将结果写回 Camera Stack 共用的原颜色目标。

通过 `plan.Add` 注册的子 Pass 自动参与初始化、准备、资源解析、停用、相机释放和最终释放。同一所有者内的同一个实例只调用一次；主 Pass 同时作为计划项时也不会重复调用。子 Pass 按注册顺序准备，因此有 CPU 准备依赖时需按依赖顺序注册；GPU 执行顺序仍由阶段和 `plan.After` 决定。不要再从 Runtime 手动调用这些子 Pass 的生命周期。原生 `ScriptableRenderPass` 的内部资源仍由其所属运行时管理。

Pass自己声明 Shader、ComputeShader、Material 或资源 ScriptableObject 引用；新增效果不向 `TsukuyomiRenderPipelineResources` 加字段。迁移的内置Pass可以继续使用既有资源资产。不要修改资产材质；创建实例并由运行时 Dispose。

`context.Frame.Resources` 已按 Feature、节点、相机隔离，可用于 `GetOrCreateHistoryTexture`、`GetOrCreateBuffer`。共享辅助服务有独立资源作用域。删除、关闭或切换 Profile 后，旧 Session 在渲染上下文结束时释放；相机销毁时调用 ReleaseCamera 并释放对应资源。原生 RHI/NGX 后端仍使用其延迟回收机制。

相机结束清理只针对本次已准备、尚未结束的渲染调用，已退役 Session 也会收到对应回调。不能因某个相机曾使用过该 Session，就在其他 Renderer 渲染它时再次恢复全局状态。上下文结束还会回收已销毁相机的资源，并处理没有后续入队的 Profile 变更。`ReleaseCamera`、`Dispose` 应释放自身持有的资源；不要在延迟回收时无条件重置全局 Shader 状态，以免覆盖新实例的状态。

共享辅助 Pass 只由已装配的消费者创建。关闭或删除最后一个 Profile 消费者后，新 Session 不再包含该服务，旧服务及其材质、相机资源在上下文结束后释放。Volume 或相机 API 暂时关闭所有消费者时，服务停止调度并清理当次状态；Session 可以保留可复用材质，重新启用时继续使用。Depth Pyramid 的全局纹理与缓冲绑定、阴影合成的临时纹理与启用标志必须在 `Deactivate` 清理；Scene Scan 的排除 Mask 请求也只属于当次相机调用，不能当作持久配置保留。

RendererFeature 的开关也约束直接调用 `AddRenderPasses`。录制中的 Session 在上下文结束后释放；Editor 空闲时禁用 Feature，不必等待另一次渲染即可清理。重复 `Create` 只保留一组事件订阅，已完成渲染的旧 Session 可立即释放。Feature 自身禁用、销毁、脚本重载及 Play Mode 退出均清理运行时；关闭 Domain Reload 时也会在模式切换边界清理并恢复。运行时缓存不参与热重载序列化，恢复后的首次入队会重新创建 Session；即使没有再次调用 `Create`，也会补齐生命周期订阅。

Scene Scan 的静态相机 API 覆盖、子效果开关及渲染器注册在进入 Play Mode 和返回 Edit Mode 时重置，避免关闭 Domain Reload 后继续使用上一轮运行的控制状态。单独禁用或重新启用 Feature 不清除这些显式相机请求。

使用 URP HistoryManager 的Pass要声明为单例，并校验 History 所有者；Renderer 切换时不能复用另一运行时留下的历史。多实例必须避免全局纹理、按类型 History 或静态可变设置冲突。

URP 会按相机渲染调用交换 History 缓冲；同帧重复渲染不能简单复用前一次的历史有效标志。SSGI 仅在帧号连续且参数、尺寸等条件兼容时使用历史，同帧重复、帧号跳跃和重置会使历史失效。

资源不足要让该节点 active=false；缺失可恢复资源可在 `PrepareRecording` 重试。初始化或准备的异常只标记对应运行时失效；不得使其他节点依赖未生产的输入。清理代码必须幂等。

## 内置模块边界

- 内置效果不再通过独立 Runtime 类转发准备调用。PCSS、Per Object Shadow、FSR3 和 Neural Rendering 由配置 Pass 自己实现 `Build`、`PrepareRecording` 和清理；其他效果的配置与录制实现位于同一个效果文件，原有序列化类型和字段保留。
- Depth Pyramid 由 GTAO/SSGI 请求，同相机、同兼容条件合并；无消费者不运行。
- Contact Shadow 节点包含生成和可选降噪；PCSS 与 Per Object Shadow 是独立节点，共享辅助阴影合成。
- VoxelTransition、ScanPostProcess、XrayRender、SpatialGridRender 是四个节点。公共排除 Mask 可以独立执行，不会因此打开 Xray 高亮。
- SceneEffectPass 只装配这些节点的共享执行计划。各录制 Pass 独立准备自己的设置和材质；排除 Mask 的消费者状态在所有准备完成后通过 `ResolveResources` 汇总。共享合成 Pass 拥有未单独排程的 Scan 参数控制器，并负责调用其准备和清理；静态相机 API 的名称保持兼容。
- Bloom 与 Tonemapping 是独立节点。相邻且顺序为 Bloom → Tonemapping 时，后处理模块合并 Uber 绘制；反序或插入其他效果时分别绘制。新后处理可以独立实现，不必修改 Uber Shader。
- Bloom、Tonemapping 的参数和材质由各自 Pass 的 `PrepareRecording` 准备；共享后处理 Pass 只组合已准备的输出，并持有自己的 Uber 材质，不再代替各效果解析参数或释放其资源。
- FSR3 主超分由 URP UpscalerRegistry 管理；Profile 中的 FSR3 Pass只提供辅助 Mask。第一人称绘制信息通过 `IRenderMaskViewProvider` 提供，支持多个有效提供者。
- DLSS Neural Rendering 由 Profile 节点管理，后端能力和 Volume 决定是否运行。编译通过不能代替 DLSS 实机验证。

## 序列化兼容

项目主 Profile 和 LookDev 示例已转换为节点配置；一次性迁移工具和旧平铺字段已清理。原始资产备份及迁移报告保留在 `Artifacts/ProfilePlugins/Migration`。不支持的 schema 和丢失的类型会阻止编辑器提交。

功能类型统一以 Pass 命名；`MovedFrom` 仅保留旧序列化类型名称，用于恢复先前保存的节点。节点 ID 与参数不随类型重命名改变。`LegacyPassDefinition` 是现有可序列化自定义 Pass 的通用适配入口，不装配任何内置效果。

## 验证

本次重构的测试程序集、示例效果和临时验证代码已清理。运行所需的结构检查、缺失资源处理和类型保留逻辑仍然存在。

提交效果前还需要真实 RenderGraph 录制和画面检查，覆盖 Game/Scene View/Preview、Camera Stack、同帧重复渲染、切换 Profile/Renderer、退出播放与资源释放。记录 CPU/GPU 基线。FSR3/DLSS 的硬件路径必须另行记录测试设备和结果。
