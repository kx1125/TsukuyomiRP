using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class ParticlesStandardGUI : ShaderProperty
{
    private Property _props = new Property();
    private Features _features = new Features();

    private static bool _RenderOption_Foldout = true;
    private static bool _Base_Foldout = true;
    private static bool _MainTex_Foldout = true;
    private static bool _NoiseTex_Foldout = true;
    private static bool _MaskTex_Foldout = true;
    private static bool _DistortionTex_Foldout = true;
    private static bool _DissolveTex_Foldout = true;
    private static bool _Fresnel_Foldout = true;

    private static Gradient gradient = new Gradient();
    private static bool RampEditor;
    
    private void UpdateProperties(MaterialProperty[] properties)
    {
        //TEXTURES
        _props.MainTex = FindProperty("_MainTex", properties);
        _props.NoiseTex = FindProperty("_NoiseTex", properties);
        _props.MaskTex = FindProperty("_MaskTex", properties);
        _props.DistortionTex = FindProperty("_DistortionTex", properties);
        _props.DissolveTex = FindProperty("_DissolveTex", properties);

        //COLORS
        _props.MainColor = FindProperty("_MainColor", properties);
        _props.DissolveColor = FindProperty("_DissolveColor", properties);
        _props.GlowColor = FindProperty("_GlowColor", properties);
        _props.FresnelColor = FindProperty("_FresnelColor", properties);
        
        //ENUMS
        _props.CullMode = FindProperty("_CullMode", properties);
        _props.SrcBlend = FindProperty("_SrcBlend", properties);
        _props.DstBlend = FindProperty("_DstBlend", properties);

        //TEXTURE OPTIONS
        //if (_props.MainTex.textureValue != null)
        {
            _props.MainTexMoveCenter = FindProperty("_MainTex_MoveCenter", properties);
            _props.MainTexRotator = FindProperty("_MainTex_Rotator", properties);
            _props.MainTexRotate = FindProperty("_MainTex_Rota", properties);
            _props.ClampMainUV = FindProperty("_ClampMainUV", properties);
        }
        
        //if (_props.NoiseTex.textureValue != null)
        {
            _props.NoiseTexMoveCenter = FindProperty("_NoiseTex_MoveCenter", properties);
            _props.NoiseTexRotator = FindProperty("_NoiseTex_Rotator", properties);
            _props.NoiseTexRotate = FindProperty("_NoiseTex_Rota", properties);
        }
        
        //if (_props.MaskTex.textureValue != null)
        {
            _props.MaskTexMoveCenter = FindProperty("_MaskTex_MoveCenter", properties);
            _props.MaskTexRotator = FindProperty("_MaskTex_Rotator", properties);
            _props.MaskTexRotate = FindProperty("_MaskTex_Rota", properties);
            _props.MaskRA = FindProperty("_MaskRA", properties);
            _props.MaskPower = FindProperty("_MaskPower", properties);
        }
        
        //if (_props.DistortionTex.textureValue != null)
        {
            _props.Distortion = FindProperty("_Distortion", properties);
            _props.DistortionTexMoveCenter = FindProperty("_DistortionTex_MoveCenter", properties);
            _props.DistortionTexRotator = FindProperty("_DistortionTex_Rotator", properties);
            _props.DistortionTexRotate = FindProperty("_DistortionTex_Rota", properties);
        }
        
        //if (_props.DissolveTex.textureValue != null)
        {
            _props.DissolveTexMoveCenter = FindProperty("_DissolveTex_MoveCenter", properties);
            _props.DissolveTexRotator = FindProperty("_DissolveTex_Rotator", properties);
            _props.DissolveTexRotate = FindProperty("_DissolveTex_Rota", properties);
            _props.DissolveProgress = FindProperty("_DissolveProgress", properties);
            _props.DissolveColor = FindProperty("_DissolveColor", properties);
            _props.DissolveRange = FindProperty("_DissolveRange", properties);
            _props.DissolveControl = FindProperty("_DissolveControl", properties);
            _props.DissolveAlphaControl = FindProperty("_DissolveAlphaControl", properties);
            _props.CenterDissolve = FindProperty("_CenterDissolve", properties);
            _props.DissolveMoveSmooth = FindProperty("_DissolveMoveSmooth", properties);
            _props.DissolveFlip = FindProperty("_DissolveFlip", properties);
            _props.DissolveAxisDir = FindProperty("_DissolveAxisDir", properties);
        }

        _props.Glow = FindProperty("_Glow", properties);
        _props.GlowColor = FindProperty("_GlowColor", properties);
        _props.Stencil = FindProperty("_Stencil", properties);
        _props.StencilComp = FindProperty("_StencilComp", properties);
        _props.StencilFailOp = FindProperty("_StencilFailOp", properties);
        _props.StencilOp = FindProperty("_StencilOp", properties);
        _props.FresnelAlphaControl = FindProperty("_FresnelAlphaControl", properties);
        _props.FresnelColor = FindProperty("_FresnelColor", properties);
        _props.FresnelRange = FindProperty("_FresnelRange", properties);
        _props.FresnelSmooth = FindProperty("_FresnelSmooth", properties);
        _props.NoiseMask = FindProperty("_NoiseMask", properties);
        _props.NoiseDistortion = FindProperty("_NoiseDistortion", properties);
        _props.NoiseDissolve = FindProperty("_NoiseDissolve", properties);
        _props.NoiseFresnel = FindProperty("_NoiseFresnel", properties);
        _props.NoiseMaskInt = FindProperty("_NoiseMaskInt", properties);
        _props.NoiseDistortionInt = FindProperty("_NoiseDistortionInt", properties);
        _props.NoiseDissolveInt = FindProperty("_NoiseDissolveInt", properties);
        _props.NoiseFresnelInt = FindProperty("_NoiseFresnelInt", properties);
        //FLOAT
    }
    

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        var material = materialEditor.target as Material;
        
        UpdateProperties(properties);
        
        using (new EditorGUILayout.VerticalScope("helpbox"))
        {
            EditorGUILayout.LabelField("Vertex Attributes", ShaderGUIHelper.SubHeaderLabel);

            if (material)
            {
                // float VertexCount = material.GetFloat("_VertexCount");
                // Vector4 AnimLength = material.GetVector("_AnimLen");
                // float StartTime = material.GetFloat("_StartTime");
                // float ChangeTime = material.GetFloat("_ChangeTime");

                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("position: POSITION.xyzw", ShaderGUIHelper.Text);
                EditorGUILayout.LabelField("color: COLOR.xyzw", ShaderGUIHelper.Text);
                
                if (_features.UseFresnel)
                {
                    EditorGUILayout.LabelField("normal: NORMAL.xyzw", ShaderGUIHelper.Text);
                }
                
                EditorGUILayout.LabelField("uv: TEXCOORD0.xy", ShaderGUIHelper.Text);

                if (_features.CustomData)
                {
                    EditorGUILayout.LabelField("dissolve progress: TEXCOORD0.z", ShaderGUIHelper.Text);
                }
                EditorGUI.indentLevel--;
            }
        }
        
        //OPTIONS
        _RenderOption_Foldout = ShaderGUIHelper.GetFoldOut(_RenderOption_Foldout, "Render Options", "渲染配置");
        if (_RenderOption_Foldout)
        {
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Cull");
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(_props.CullMode, new GUIContent("CullMode", "裁剪模式"));
                EditorGUI.indentLevel--;
            }

            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("AlphaBlend");
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(_props.SrcBlend, new GUIContent("SrcBlend", "源透明度模式"));
                materialEditor.ShaderProperty(_props.DstBlend, new GUIContent("DstBlend", "目标透明度模式"));
                EditorGUI.indentLevel--;
            }
            
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Stencil");
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(_props.Stencil, new GUIContent("Stencil ID", "模板值"));
                materialEditor.ShaderProperty(_props.StencilComp, new GUIContent("StencilComp", "模板值比较方式"));
                materialEditor.ShaderProperty(_props.StencilOp, new GUIContent("StencilOp", "模板测试通过操作"));
                materialEditor.ShaderProperty(_props.StencilFailOp, new GUIContent("StencilFailOp", "模板测试失败操作"));
                EditorGUI.indentLevel--;
            }
            
            // _features.ForceField = material.IsKeywordEnabled("_FORCEFIELD_ON");
            // _features.Ripple = material.IsKeywordEnabled("_RIPPLE_ON");
            // _features.ArrowLine = material.IsKeywordEnabled("_ARROWLINE_ON");
            // EditorGUI.BeginChangeCheck();
            // {
            //     materialEditor.ShaderProperty(_props.CullMode, new GUIContent("CullMode", "裁剪模式"));
            //     _features.ForceField = EditorGUILayout.Toggle(new GUIContent("ForceField", "开启力场效果"), _features.ForceField);
            //     _features.ArrowLine = EditorGUILayout.Toggle(new GUIContent("ArrowLine", "开启连线"), _features.ArrowLine);
            //     _features.Ripple = EditorGUILayout.Toggle(new GUIContent("Ripple", "开启涟漪效果"), _features.Ripple);
            // }
            // if(EditorGUI.EndChangeCheck())
            // {
            //     ApplyKeyworld(material, "_FORCEFIELD_ON", _features.ForceField);
            //     ApplyKeyworld(material, "_RIPPLE_ON", _features.Ripple);
            //     ApplyKeyworld(material, "_ARROWLINE_ON", _features.ArrowLine);
            // }

            //EditorGUI.indentLevel--;
        }
        
        _Base_Foldout = ShaderGUIHelper.GetFoldOut(_Base_Foldout, "Main Settings", "主要属性");
        if (_Base_Foldout)
        {
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Color");
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(_props.MainColor, new GUIContent("MainColor", "主颜色"));
                EditorGUI.indentLevel--;
            }
            
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Emission");
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(_props.Glow, new GUIContent("Emission Intensity", "自发光强度"));
                materialEditor.ShaderProperty(_props.GlowColor, new GUIContent("Emission Color", "自发光颜色"));
                EditorGUI.indentLevel--;
            }
        }

        _MainTex_Foldout = ShaderGUIHelper.GetFoldOut(_MainTex_Foldout, "MainColor", "主帖图设置");
        if (_MainTex_Foldout)
        {
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("BaseMap", "主贴图"), _props.MainTex);
                if (_props.MainTex.textureValue != null)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.TextureScaleOffsetProperty(_props.MainTex);
                    EditorGUI.indentLevel--;
                }
            }

            if (_props.MainTex.textureValue != null)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    Vector4 moveCenter = _props.MainTexMoveCenter.vectorValue;
                    Vector2 move = new Vector2(moveCenter.x, moveCenter.y);
                    Vector2 center = new Vector2(moveCenter.z, moveCenter.w);
                    
                    ShaderGUIHelper.GetSubHeader("MainTex Settings");
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(_props.ClampMainUV, new GUIContent("ClampMainUV", "限制UV到0-1"));
                    materialEditor.ShaderProperty(_props.MainTexRotator, new GUIContent("UV Animation", "移动或旋转贴图"));
                    if (material.IsKeywordEnabled("_MAINTEX_ROTATOR_MOVE_MAINTEX"))
                    {
                        move = EditorGUILayout.Vector2Field(new GUIContent("Move Speed", "移动速度"), move);
                    }
                    else
                    {
                        center = EditorGUILayout.Vector2Field(new GUIContent("RotateCenter", "旋转中心"), center);
                        materialEditor.ShaderProperty(_props.MainTexRotate, new GUIContent("RotateSpeed", "旋转速度"));
                    }
                    _props.MainTexMoveCenter.vectorValue = new Vector4(move.x, move.y, center.x, center.y);
                    EditorGUI.indentLevel--;
                }
            }
            
            
        }
        
        _NoiseTex_Foldout = ShaderGUIHelper.GetFoldOut(_NoiseTex_Foldout, "Noise", "噪声纹理设置");
        if (_NoiseTex_Foldout)
        {
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("NoiseTex", "噪声图"), _props.NoiseTex);
                if (_props.NoiseTex.textureValue != null)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.TextureScaleOffsetProperty(_props.NoiseTex);
                    EditorGUI.indentLevel--;
                }
            }

            if (_props.NoiseTex.textureValue != null)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    Vector4 moveCenter = _props.NoiseTexMoveCenter.vectorValue;
                    Vector2 move = new Vector2(moveCenter.x, moveCenter.y);
                    Vector2 center = new Vector2(moveCenter.z, moveCenter.w);
                    
                    ShaderGUIHelper.GetSubHeader("NoiseTex Settings");
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(_props.NoiseTexRotator, new GUIContent("UV Animation", "移动或旋转贴图"));
                    if (material.IsKeywordEnabled("_NOISETEX_ROTATOR_MOVE_NOISETEX"))
                    {
                        move = EditorGUILayout.Vector2Field(new GUIContent("Move Speed", "移动速度"), move);
                    }
                    else
                    {
                        center = EditorGUILayout.Vector2Field(new GUIContent("RotateCenter", "旋转中心"), center);
                        materialEditor.ShaderProperty(_props.NoiseTexRotate, new GUIContent("RotateSpeed", "旋转速度"));
                    }
                    _props.NoiseTexMoveCenter.vectorValue = new Vector4(move.x, move.y, center.x, center.y);
                    EditorGUI.indentLevel--;
                }
            }
            
            
        }
        
        _MaskTex_Foldout = ShaderGUIHelper.GetFoldOut(_MaskTex_Foldout, "Mask", "遮罩纹理设置");
        if (_MaskTex_Foldout)
        {
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("MaskTex", "噪声图"), _props.MaskTex);
                if (_props.MaskTex.textureValue != null)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.TextureScaleOffsetProperty(_props.MaskTex);
                    EditorGUI.indentLevel--;
                }
            }

            if (_props.MaskTex.textureValue != null)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    Vector4 moveCenter = _props.MaskTexMoveCenter.vectorValue;
                    Vector2 move = new Vector2(moveCenter.x, moveCenter.y);
                    Vector2 center = new Vector2(moveCenter.z, moveCenter.w);
                    
                    ShaderGUIHelper.GetSubHeader("MaskTex Settings");
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(_props.MaskRA, new GUIContent("Use Alpha Channel","使用Alpha通道作为遮罩(默认为R通道)"));
                    materialEditor.ShaderProperty(_props.MaskPower, new GUIContent("Mask Power", "遮罩范围"));
                    materialEditor.ShaderProperty(_props.MaskTexRotator, new GUIContent("UV Animation", "移动或旋转贴图"));
                    if (material.IsKeywordEnabled("_MASKTEX_ROTATOR_MOVE_MASKTEX"))
                    {
                        move = EditorGUILayout.Vector2Field(new GUIContent("Move Speed", "移动速度"), move);
                    }
                    else
                    {
                        center = EditorGUILayout.Vector2Field(new GUIContent("RotateCenter", "旋转中心"), center);
                        materialEditor.ShaderProperty(_props.MaskTexRotate, new GUIContent("RotateSpeed", "旋转速度"));
                    }
                    _props.MaskTexMoveCenter.vectorValue = new Vector4(move.x, move.y, center.x, center.y);
                    EditorGUI.indentLevel--;
                }

                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    EditorGUILayout.LabelField("Noise", ShaderGUIHelper.SubHeaderLabel);
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(_props.NoiseMask, new GUIContent("Use Noise","使用噪声图影响遮罩"));
                    EditorGUI.BeginDisabledGroup(_props.NoiseMask.floatValue < 0.5f);
                    materialEditor.ShaderProperty(_props.NoiseMaskInt,
                        new GUIContent("Noise Intensity", "噪声强度"));
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
            }
            
        }
        
        _DistortionTex_Foldout = ShaderGUIHelper.GetFoldOut(_DistortionTex_Foldout, "Distortion", "扭曲纹理设置");
        if (_DistortionTex_Foldout)
        {
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("DistortionTex", "扭曲贴图"), _props.DistortionTex);
                if (_props.DistortionTex.textureValue != null)
                {
                    EditorGUI.indentLevel++;
                    materialEditor.TextureScaleOffsetProperty(_props.DistortionTex);
                    EditorGUI.indentLevel--;
                }
            }

            if (_props.DistortionTex.textureValue != null)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    Vector4 moveCenter = _props.DistortionTexMoveCenter.vectorValue;
                    Vector2 move = new Vector2(moveCenter.x, moveCenter.y);
                    Vector2 center = new Vector2(moveCenter.z, moveCenter.w);
                    
                    ShaderGUIHelper.GetSubHeader("DistortionTex Settings");
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(_props.Distortion, new GUIContent("Distortion Intensity", "扭曲强度"));
                    materialEditor.ShaderProperty(_props.DistortionTexRotator, new GUIContent("UV Animation", "移动或旋转贴图"));
                    if (material.IsKeywordEnabled("_DISTORTIONTEX_ROTATOR_MOVE_DISTORTIONTEX"))
                    {
                        move = EditorGUILayout.Vector2Field(new GUIContent("Move Speed", "移动速度"), move);
                    }
                    else
                    {
                        center = EditorGUILayout.Vector2Field(new GUIContent("RotateCenter", "旋转中心"), center);
                        materialEditor.ShaderProperty(_props.DistortionTexRotate, new GUIContent("RotateSpeed", "旋转速度"));
                    }
                    _props.DistortionTexMoveCenter.vectorValue = new Vector4(move.x, move.y, center.x, center.y);
                    EditorGUI.indentLevel--;
                }
                
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    EditorGUILayout.LabelField("Noise", ShaderGUIHelper.SubHeaderLabel);
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(_props.NoiseDistortion, new GUIContent("Use Noise","使用噪声图影响扭曲"));
                    EditorGUI.BeginDisabledGroup(_props.NoiseDistortion.floatValue < 0.5f);
                    materialEditor.ShaderProperty(_props.NoiseDistortionInt,
                        new GUIContent("Noise Intensity", "噪声强度"));
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
            }
            
        }

        _DissolveTex_Foldout = ShaderGUIHelper.GetFoldOut(_DissolveTex_Foldout, "Dissolve", "溶解纹理设置");
        if (_DissolveTex_Foldout)
        {
            _features.UseDissolve = material.IsKeywordEnabled("_USEDISSOLVE");
            EditorGUI.BeginChangeCheck();
            {
                _features.UseDissolve = EditorGUILayout.Toggle(new GUIContent("UseDissolve", "使用溶解效果"), _features.UseDissolve);
            }
            if(EditorGUI.EndChangeCheck())
            {
                ApplyKeyworld(material, "_USEDISSOLVE", _features.UseDissolve);
            }
            
            if (_features.UseDissolve)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("DissolveTex", "溶解贴图"), _props.DissolveTex);
                    if (_props.DissolveTex.textureValue != null)
                    {
                        EditorGUI.indentLevel++;
                        materialEditor.TextureScaleOffsetProperty(_props.DissolveTex);
                        EditorGUI.indentLevel--;
                    }
                }

                if (_props.DissolveTex.textureValue != null)
                {
                    using (new EditorGUILayout.VerticalScope("helpbox"))
                    {

                        Vector4 moveCenter = _props.DissolveTexMoveCenter.vectorValue;
                        Vector2 move = new Vector2(moveCenter.x, moveCenter.y);
                        Vector2 center = new Vector2(moveCenter.z, moveCenter.w);

                        ShaderGUIHelper.GetSubHeader("DissolveTex Settings");
                        EditorGUI.indentLevel++;
                        
                        _features.CustomData = material.IsKeywordEnabled("_USECUSTOMDATA");
                        EditorGUI.BeginChangeCheck();
                        {
                            _features.CustomData = EditorGUILayout.Toggle(new GUIContent("UseCustomData", "使用自定义数据控制溶解进度(TEXCOORD0.z)"), _features.CustomData);
                        }
                        if(EditorGUI.EndChangeCheck())
                        {
                            ApplyKeyworld(material, "_USECUSTOMDATA", _features.CustomData);
                        }
                        
                        EditorGUI.BeginDisabledGroup(_features.CustomData);
                        materialEditor.ShaderProperty(_props.DissolveProgress,
                            new GUIContent("Dissolve Progress", "溶解进度"));
                        EditorGUI.EndDisabledGroup();
                        
                        materialEditor.ShaderProperty(_props.DissolveAlphaControl, new GUIContent("Dissolve Control Alpha", "溶解影响透明度(边缘半透明)"));
                        materialEditor.ShaderProperty(_props.DissolveFlip,
                            new GUIContent("Dissolve Progress Flip", "反转溶解进度"));
                        materialEditor.ShaderProperty(_props.DissolveTexRotator,
                            new GUIContent("UV Animation", "移动或旋转贴图"));
                        if (material.IsKeywordEnabled("_DISSOLVETEX_ROTATOR_MOVE_DISSOLVETEX"))
                        {
                            move = EditorGUILayout.Vector2Field(new GUIContent("Move Speed", "移动速度"), move);
                        }
                        else
                        {
                            center = EditorGUILayout.Vector2Field(new GUIContent("RotateCenter", "旋转中心"), center);
                            materialEditor.ShaderProperty(_props.DissolveTexRotate,
                                new GUIContent("RotateSpeed", "旋转速度"));
                        }

                        _props.DissolveTexMoveCenter.vectorValue = new Vector4(move.x, move.y, center.x, center.y);
                        EditorGUI.indentLevel--;
                    }
                    
                    using (new EditorGUILayout.VerticalScope("helpbox"))
                    {
                        ShaderGUIHelper.GetSubHeader("Dissolve Edge Settings");
                        EditorGUI.indentLevel++;
                        materialEditor.ShaderProperty(_props.DissolveColor,
                            new GUIContent("Dissolve Edge Color", "溶解边缘颜色"));
                        materialEditor.ShaderProperty(_props.DissolveRange,
                            new GUIContent("Dissolve Edge Range", "溶解边缘范围"));
                        materialEditor.ShaderProperty(_props.DissolveMoveSmooth,
                            new GUIContent("Dissolve Edge Smoothness", "溶解边缘锐度"));
                        EditorGUI.indentLevel--;
                    }
                    
                    using (new EditorGUILayout.VerticalScope("helpbox"))
                    {
                        ShaderGUIHelper.GetSubHeader("Dissolve Direction Settings");
                        EditorGUI.indentLevel++;
                        materialEditor.ShaderProperty(_props.DissolveControl,
                            new GUIContent("Dissolve Control", "溶解方向性强度"));
                        materialEditor.ShaderProperty(_props.CenterDissolve, new GUIContent("Center Dissolve", "中心溶解"));
                        materialEditor.ShaderProperty(_props.DissolveAxisDir, new GUIContent("Dissolve Direciton", "溶解方向(x,y为0时无方向性)"));
                        EditorGUI.indentLevel--;
                    }
                    
                    using (new EditorGUILayout.VerticalScope("helpbox"))
                    {
                        EditorGUILayout.LabelField("Noise", ShaderGUIHelper.SubHeaderLabel);
                        EditorGUI.indentLevel++;
                        materialEditor.ShaderProperty(_props.NoiseDissolve, new GUIContent("Use Noise","使用噪声图影响溶解"));
                        EditorGUI.BeginDisabledGroup(_props.NoiseDissolve.floatValue < 0.5f);
                        materialEditor.ShaderProperty(_props.NoiseDissolveInt,
                            new GUIContent("Noise Intensity", "噪声强度"));
                        EditorGUI.EndDisabledGroup();
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }
        
        _Fresnel_Foldout = ShaderGUIHelper.GetFoldOut(_Fresnel_Foldout, "Fresnel", "菲涅尔边缘光效果(需要使用mesh粒子)");
        if (_Fresnel_Foldout)
        {
            _features.UseFresnel = material.IsKeywordEnabled("_USEFRESNEL");
            EditorGUI.BeginChangeCheck();
            {
                _features.UseFresnel = EditorGUILayout.Toggle(new GUIContent("UseFresnel", "使用菲涅尔效果"), _features.UseFresnel);
            }
            if(EditorGUI.EndChangeCheck())
            {
                ApplyKeyworld(material, "_USEFRESNEL", _features.UseFresnel);
            }
            
            if (_features.UseFresnel)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    materialEditor.ShaderProperty(_props.FresnelAlphaControl,
                        new GUIContent("Fresnel Control Alpha", "菲涅尔影响透明度"));
                    materialEditor.ShaderProperty(_props.FresnelColor, new GUIContent("Fresnel Color", "菲涅尔颜色"));
                    materialEditor.ShaderProperty(_props.FresnelRange, new GUIContent("Fresnel Range", "菲涅尔范围"));
                    materialEditor.ShaderProperty(_props.FresnelSmooth, new GUIContent("Fresnel Smoothness", "菲涅尔锐度"));
                }
                
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    EditorGUILayout.LabelField("Noise", ShaderGUIHelper.SubHeaderLabel);
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(_props.NoiseFresnel, new GUIContent("Use Noise","使用噪声图影响菲涅尔"));
                    EditorGUI.BeginDisabledGroup(_props.NoiseFresnel.floatValue < 0.5f);
                    materialEditor.ShaderProperty(_props.NoiseFresnelInt,
                        new GUIContent("Noise Intensity", "噪声强度"));
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
            }
        }

        base.OnGUI(materialEditor, new MaterialProperty[] { });
    }
}
