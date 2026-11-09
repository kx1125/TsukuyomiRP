using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    [InjectionPoint(InjectionPoint.AfterOpaque)]
    public sealed class ObjectOutlinePass : RenderObjectPass
    {
        private static readonly int DistanceFadeParamsId = Shader.PropertyToID("_TsukuyomiOutlineDistanceFadeParams");
        private bool _volumeEnabled = true;
        private Vector4 _distanceFadeParams;

        public override string Name => "ObjectOutlinePass";

        public ObjectOutlinePass()
        {
            InjectionPoint = InjectionPoint.AfterOpaque;
            CullingMask = -1;
            RenderTransparent = false;
        }

        protected override void PopulateShaderTagIds(List<ShaderTagId> shaderTagIds)
        {
            shaderTagIds.Add(new ShaderTagId("ObjectOutline"));
        }

        protected override bool Render(RenderObjectContext ctx)
        {
            ctx.UseCameraView();
            return true;
        }

        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
        {
            var volume = context.Volumes?.GetComponent<TsukuyomiObjectOutlineVolume>();
            bool hasVolume = volume != null && volume.active;
            _volumeEnabled = !hasVolume || volume.enable.value;
            _distanceFadeParams = Vector4.zero;
            if (!hasVolume)
                return;

            // The stack resets override flags when it copies values from the pipeline's default profile.
            if (!volume.HasFadeOverride)
            {
                var defaults = VolumeManager.instance.GetVolumeComponentDefaultState(typeof(TsukuyomiObjectOutlineVolume)) as TsukuyomiObjectOutlineVolume;
                if (defaults == null || !defaults.HasFadeOverride)
                    return;
            }

            float end = volume.distanceFadeEnd.value;
            float range = end - volume.distanceFadeStart.value;
            _distanceFadeParams = new Vector4(end, range > 0.0f ? 1.0f / range : 0.0f, 1.0f, 0.0f);
        }

        public override bool IsActive(in FrameContext frame)
        {
            return _volumeEnabled && base.IsActive(frame);
        }

        protected override void RecordCameraDraw(in RasterPassContext context, RendererListHandle opaque, RendererListHandle transparent)
        {
            context.PassData.parameters = _distanceFadeParams;
            context.Builder.AllowGlobalStateModification(true);
            context.SetRenderFunc((data, graphContext) =>
            {
                graphContext.cmd.SetGlobalVector(DistanceFadeParamsId, data.parameters);
                DrawRendererLists(graphContext, opaque, transparent);
            });
        }
    }
}
