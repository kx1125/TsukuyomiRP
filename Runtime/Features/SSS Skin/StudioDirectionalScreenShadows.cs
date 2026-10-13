using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    public static class StudioDirectionalScreenShadows
    {
        sealed class Data {public Material material;}
        public static void Record(RenderGraph graph,ContextContainer frameData,Material material,TextureHandle shadow)
        {
            var camera=frameData.Get<UniversalCameraData>();
            var texture=graph.CreateTexture(new TextureDesc(camera.cameraTargetDescriptor.width,camera.cameraTargetDescriptor.height)
            {name="Studio original R8 screen shadows",colorFormat=GraphicsFormat.R8_UNorm,filterMode=FilterMode.Bilinear});
            using(var builder=graph.AddRasterRenderPass<Data>("Studio original directional screen shadows",out var data))
            {
                data.material=material;builder.UseTexture(shadow);builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(texture,0);builder.SetGlobalTextureAfterPass(texture,Shader.PropertyToID("_StudioDirectionalScreenShadowTexture"));
                builder.AllowGlobalStateModification(true);builder.AllowPassCulling(false);
                builder.SetRenderFunc((Data value,RasterGraphContext context)=>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity,value.material,0,MeshTopology.Triangles,3,1);
                    context.cmd.SetGlobalFloat("_StudioDirectionalScreenShadowReady",1);
                });
            }
        }
    }
}
