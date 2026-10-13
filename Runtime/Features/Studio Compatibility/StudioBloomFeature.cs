using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    // PPSv2 adds each pyramid level; URP's normalized scatter has different energy.
    public sealed class StudioBloomFeature : ScriptableRendererFeature
    {
        public Material material;
        public float diffusion=7.941558f, anamorphicRatio=1, intensity=1.1038959f;
        public Color tint=new Color(.7490196f,.7490196f,.7490196f);
        Pass pass;
        public override void Create() { pass=new Pass(this); }
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if(!material || !data.cameraData.postProcessEnabled || data.cameraData.cameraType==CameraType.Preview || data.cameraData.cameraType==CameraType.Reflection)return;
            renderer.EnqueuePass(pass);
        }
        sealed class Pass:ScriptableRenderPass
        {
            readonly StudioBloomFeature settings;
            readonly int[] down=new int[16],up=new int[16],width=new int[16],height=new int[16];
            public Pass(StudioBloomFeature settings)
            {
                this.settings=settings;renderPassEvent=RenderPassEvent.BeforeRenderingPostProcessing;
                for(int i=0;i<16;i++){down[i]=Shader.PropertyToID("_StudioBloomDown"+i);up[i]=Shader.PropertyToID("_StudioBloomUp"+i);}
            }
            sealed class Data {public TextureHandle color;public RenderTextureDescriptor descriptor;public Pass pass;}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var resources=frame.Get<UniversalResourceData>();var camera=frame.Get<UniversalCameraData>();
                using(var builder=graph.AddUnsafePass<Data>("Studio Additive Bloom",out var data))
                {
                    data.color=resources.activeColorTexture;data.descriptor=camera.cameraTargetDescriptor;data.pass=this;
                    builder.UseTexture(data.color,AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);builder.AllowPassCulling(false);
                    builder.SetRenderFunc((Data d,UnsafeGraphContext ctx)=>
                    {
                        var cmd=CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        RTHandle color=d.color;d.pass.Render(cmd,color.nameID,d.descriptor);
                    });
                }
            }
            void Draw(CommandBuffer cmd,RenderTargetIdentifier source,int sw,int sh,RenderTargetIdentifier destination,int w,int h,int shaderPass,MaterialPropertyBlock properties)
            {
                cmd.SetGlobalTexture("_MainTex",source);
                cmd.SetGlobalVector("_MainTex_TexelSize",new Vector4(1f/sw,1f/sh,sw,sh));
                cmd.SetRenderTarget(destination);cmd.SetViewport(new Rect(0,0,w,h));
                cmd.DrawProcedural(Matrix4x4.identity,settings.material,shaderPass,MeshTopology.Triangles,3,1,properties);
            }
            void Render(CommandBuffer cmd,RenderTargetIdentifier color,RenderTextureDescriptor descriptor)
            {
                float ratio=Mathf.Clamp(settings.anamorphicRatio,-1,1);
                int w=Mathf.Max(1,Mathf.FloorToInt(descriptor.width/(2-Mathf.Max(-ratio,0))));
                int h=Mathf.Max(1,Mathf.FloorToInt(descriptor.height/(2-Mathf.Max(ratio,0))));
                float logs=Mathf.Log(Mathf.Max(w,h),2)+Mathf.Min(settings.diffusion,10)-10;
                int iterations=Mathf.Clamp(Mathf.FloorToInt(logs),1,16);
                var props=new MaterialPropertyBlock();
                props.SetFloat("_SampleScale",.5f+logs-Mathf.FloorToInt(logs));
                props.SetVector("_Threshold",new Vector4(0,-1e-5f,2e-5f,25000));
                props.SetVector("_Params",new Vector4(65504,0,0,0));
                Color tint=settings.tint.linear;
                props.SetVector("_ColorIntensity",new Vector4(tint.r,tint.g,tint.b,Mathf.Pow(2,settings.intensity/10)-1));
                // Match BloomRenderer's SetColor binding on the original Uber pass.
                props.SetColor("_StudioBloomTint",tint);
                var desc=descriptor;desc.depthBufferBits=0;desc.msaaSamples=1;desc.colorFormat=RenderTextureFormat.ARGBHalf;desc.sRGB=false;
                RenderTargetIdentifier last=color;int lastW=descriptor.width,lastH=descriptor.height;
                for(int i=0;i<iterations;i++)
                {
                    width[i]=w;height[i]=h;desc.width=w;desc.height=h;
                    cmd.GetTemporaryRT(down[i],desc,FilterMode.Bilinear);cmd.GetTemporaryRT(up[i],desc,FilterMode.Bilinear);
                    Draw(cmd,last,lastW,lastH,down[i],w,h,i==0?0:2,props);
                    last=down[i];lastW=w;lastH=h;w=Mathf.Max(w/2,1);h=Mathf.Max(h/2,1);
                }
                for(int i=iterations-2;i>=0;i--)
                {
                    cmd.SetGlobalTexture("_BloomTex",down[i]);
                    Draw(cmd,last,lastW,lastH,up[i],width[i],height[i],4,props);
                    last=up[i];lastW=width[i];lastH=height[i];
                }
                Draw(cmd,last,lastW,lastH,color,descriptor.width,descriptor.height,7,props);
                for(int i=0;i<iterations;i++){cmd.ReleaseTemporaryRT(down[i]);cmd.ReleaseTemporaryRT(up[i]);}
            }
        }
    }
}
