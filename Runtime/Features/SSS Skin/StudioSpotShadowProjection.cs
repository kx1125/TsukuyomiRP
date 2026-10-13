using System;
using System.Reflection;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    // Unity 6 clamps a spotlight's native near plane to 1% of its range.
    // The captured Studio light uses 0.1 source units (0.01 imported units).
    // Preserve its depth quantization instead of compensating only caster bias.
    internal static class StudioSpotShadowProjection
    {
        static readonly FieldInfo CullingInfos=typeof(UniversalShadowData).GetField("visibleLightsShadowCullingInfos",BindingFlags.Instance|BindingFlags.NonPublic);
        public static bool Apply(UniversalShadowData shadowData,UniversalLightData lights,float sourceNear,out Matrix4x4 firstShadow,out Vector3 firstPosition)
        {
            bool found=false;firstShadow=Matrix4x4.identity;firstPosition=Vector3.zero;
            if(CullingInfos==null)throw new InvalidOperationException("Installed URP spotlight projection adapter is unavailable.");
            var infos=CullingInfos.GetValue(shadowData);
            for(int i=0;i<lights.visibleLights.Length;i++)
            {
                var visible=lights.visibleLights[i];var light=visible.light;
                if(visible.lightType!=LightType.Spot||!light||light.shadows==LightShadows.None)continue;
                float far=light.range,near=Mathf.Clamp(sourceNear,.000001f,far*.5f);
                var info=infos.GetType().GetProperty("Item").GetValue(infos,new object[]{i});
                var slices=(NativeArray<ShadowSliceData>)info.GetType().GetField("slices").GetValue(info);
                if(slices.Length==0)continue;
                var slice=slices[0];var projection=slice.projectionMatrix;
                projection.m22=-(far+near)/(far-near);
                projection.m23=-2*far*near/(far-near);
                slice.projectionMatrix=projection;
                // Match the installed ShadowUtils.GetShadowTransform exactly.
                if(SystemInfo.usesReversedZBuffer)
                {projection.m20=-projection.m20;projection.m21=-projection.m21;projection.m22=-projection.m22;projection.m23=-projection.m23;}
                var texture=Matrix4x4.identity;
                texture.m00=texture.m11=texture.m22=.5f;
                texture.m03=texture.m13=texture.m23=.5f;
                slice.shadowTransform=texture*(projection*slice.viewMatrix);
                slices[0]=slice;
                if(!found){firstShadow=slice.shadowTransform;firstPosition=light.transform.position;found=true;}
            }
            return found;
        }
    }
}
