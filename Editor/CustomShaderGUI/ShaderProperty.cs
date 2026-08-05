using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ShaderProperty : ShaderGUI
{
    public class Features
    {
        public bool CustomData;
        public bool CenterDissolve;
        public bool UseFresnel;
        public bool UseDissolve;
        public bool SoftParticles;
    }

    public class Property
    {
        //FLOAT4
        public MaterialProperty MainTexMoveCenter;
        public MaterialProperty NoiseTexMoveCenter;
        public MaterialProperty MaskTexMoveCenter;
        public MaterialProperty DistortionTexMoveCenter;
        public MaterialProperty DissolveTexMoveCenter;
        public MaterialProperty DissolveAxisDir;
        
        //FLOAT
        public MaterialProperty MainTexRotate;
        public MaterialProperty NoiseTexRotate;
        public MaterialProperty MaskTexRotate;
        public MaterialProperty DistortionTexRotate;
        public MaterialProperty DissolveTexRotate;
        
        //COLOR
        public MaterialProperty MainColor;
        public MaterialProperty DissolveColor;
        public MaterialProperty GlowColor;
        public MaterialProperty FresnelColor;
        
        //HALF
        public MaterialProperty Distortion;
        public MaterialProperty DissolveFlip;
        public MaterialProperty CenterDissolve;
        public MaterialProperty DissolveMoveSmooth;
        public MaterialProperty DissolveControl;
        public MaterialProperty DissolveProgress;
        public MaterialProperty DissolveRange;
        public MaterialProperty DissolveAlphaControl;
        public MaterialProperty Glow;
        public MaterialProperty FresnelRange;
        public MaterialProperty FresnelSmooth;
        public MaterialProperty FresnelAlphaControl;
        public MaterialProperty MaskPower;
        public MaterialProperty NoiseMask;
        public MaterialProperty NoiseDistortion;
        public MaterialProperty NoiseDissolve;
        public MaterialProperty NoiseFresnel;
        public MaterialProperty NoiseMaskInt;
        public MaterialProperty NoiseDistortionInt;
        public MaterialProperty NoiseDissolveInt;
        public MaterialProperty NoiseFresnelInt;
        public MaterialProperty SoftParticlesNearFadeDistance;
        public MaterialProperty SoftParticlesFarFadeDistance;
        public MaterialProperty SoftParticleFadeParams;
        
        //INT
        public MaterialProperty CullMode;
        public MaterialProperty MainTexRotator;
        public MaterialProperty NoiseTexRotator;
        public MaterialProperty DistortionTexRotator;
        public MaterialProperty MaskTexRotator;
        public MaterialProperty DissolveTexRotator;
        public MaterialProperty NoiseTexChannel;
        public MaterialProperty MaskTexChannel;
        public MaterialProperty DistortionTexChannel;
        public MaterialProperty DissolveTexChannel;
        public MaterialProperty SoftParticlesEnabled;
        public MaterialProperty SrcBlend;
        public MaterialProperty DstBlend;
        public MaterialProperty StencilComp;
        public MaterialProperty Stencil;
        public MaterialProperty StencilOp;
        public MaterialProperty StencilFailOp;
        public MaterialProperty ClampMainUV;
        
        //TEXTURE
        public MaterialProperty MainTex;
        public MaterialProperty NoiseTex;
        public MaterialProperty MaskTex;
        public MaterialProperty DistortionTex;
        public MaterialProperty DissolveTex;
    }
    
    public static void ApplyKeyworld(Material material, string keyword, bool toggle)
    {
        if(toggle)
            material.EnableKeyword(keyword);
        else 
            material.DisableKeyword(keyword);
    }
}
