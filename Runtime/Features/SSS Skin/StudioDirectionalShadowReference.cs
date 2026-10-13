using System;
using System.Reflection;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace Tsukuyomi.Rendering
{
    // Native captured shadow-camera parameters are used only for the matching
    // reference camera. Other camera poses keep the normal URP projection.
    [Serializable] public sealed class StudioDirectionalShadowReference
    {
        [Serializable] public sealed class MatrixData
        {
            public float[] values;
            public Matrix4x4 Matrix {get {var m=new Matrix4x4();for(int r=0;r<4;r++)for(int c=0;c<4;c++)m[r,c]=values[r*4+c];return m;}}
        }
        public MatrixData[] casterGPU,worldToShadow;
        public Vector4[] casterBias;
        public Vector4 splitNear,splitFar;
        public Vector3 cameraPosition,lightDirection;
        public Quaternion cameraRotation;
        public float fieldOfView,aspect,shadowStrength;
        static readonly FieldInfo Slices=typeof(MainLightShadowCasterPass).GetField("m_CascadeSlices",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo CullingInfos=typeof(UniversalShadowData).GetField("visibleLightsShadowCullingInfos",BindingFlags.Instance|BindingFlags.NonPublic);
        public bool Matches(Camera camera,Light light)
        {
            return camera && light && Vector3.Distance(camera.transform.position,cameraPosition)<.00001f
                && Quaternion.Angle(camera.transform.rotation,cameraRotation)<.001f
                && Mathf.Abs(camera.fieldOfView-fieldOfView)<.0001f && Mathf.Abs(camera.aspect-aspect)<.0001f
                && Vector3.Distance(light.transform.forward,lightDirection)<.00001f
                && (SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
                    || SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Direct3D12);
        }
        public void Apply(MainLightShadowCasterPass pass)
        {
            if(Slices==null)throw new InvalidOperationException("Installed URP cascade adapter is unavailable.");
            var slices=(ShadowSliceData[])Slices.GetValue(pass);
            var inverseGPU=GL.GetGPUProjectionMatrix(Matrix4x4.identity,true).inverse;
            for(int i=0;i<slices.Length;i++)
            {
                slices[i].viewMatrix=Matrix4x4.identity;
                var caster=casterGPU[Mathf.Min(i,casterGPU.Length-1)].Matrix;
                // Native frame has no geometry in the last two tiles.
                if(i>=casterGPU.Length)caster.m03+=100;
                slices[i].projectionMatrix=inverseGPU*caster;
                slices[i].shadowTransform=worldToShadow[i].Matrix;
            }
        }
        public void ApplyCulling(UniversalShadowData shadowData,int lightIndex)
        {
            // Update the captured camera parameters before URP records its own
            // main shadow pass; no extra color-camera shadow draw is required.
            var infos=CullingInfos.GetValue(shadowData);
            var info=infos.GetType().GetProperty("Item").GetValue(infos,new object[]{lightIndex});
            var slices=(NativeArray<ShadowSliceData>)info.GetType().GetField("slices").GetValue(info);
            var inverseGPU=GL.GetGPUProjectionMatrix(Matrix4x4.identity,true).inverse;
            for(int i=0;i<slices.Length;i++)
            {
                var slice=slices[i];
                slice.viewMatrix=Matrix4x4.identity;
                var caster=casterGPU[Mathf.Min(i,casterGPU.Length-1)].Matrix;
                if(i>=casterGPU.Length)caster.m03+=100;
                slice.projectionMatrix=inverseGPU*caster;
                slice.shadowTransform=worldToShadow[i].Matrix;
                slices[i]=slice;
            }
        }
        public void SetGlobals(UnityEngine.Rendering.RenderGraphModule.RasterGraphContext context)
        {
            var matrices=new Matrix4x4[4];for(int i=0;i<4;i++)matrices[i]=worldToShadow[i].Matrix;
            context.cmd.SetGlobalMatrixArray("_StudioDirectionalWorldToShadow",matrices);
            context.cmd.SetGlobalVectorArray("_StudioDirectionalCasterBias",casterBias);
            context.cmd.SetGlobalVector("_StudioDirectionalSplitNear",splitNear);
            context.cmd.SetGlobalVector("_StudioDirectionalSplitFar",splitFar);
            context.cmd.SetGlobalFloat("_StudioDirectionalShadowStrength",shadowStrength);
            context.cmd.SetGlobalFloat("_StudioCapturedDirectionalShadowReady",1);
        }
    }
}
