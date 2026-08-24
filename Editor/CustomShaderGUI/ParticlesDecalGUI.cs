using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class ParticlesDecalGUI : ShaderGUI
{
    private static readonly List<ParticleSystemVertexStream> RequiredStreams = new()
    {
        ParticleSystemVertexStream.Position,
        ParticleSystemVertexStream.Color,
        ParticleSystemVertexStream.UV,
        ParticleSystemVertexStream.AnimFrame,
        ParticleSystemVertexStream.Custom1X
    };

    private static bool s_ProjectionFoldout = true;
    private static bool s_RenderFoldout = true;
    private static bool s_MainSettingsFoldout = true;
    private static bool s_MainTexFoldout = true;
    private static bool s_NoiseFoldout = true;
    private static bool s_MaskFoldout = true;
    private static bool s_DistortionFoldout = true;
    private static bool s_DissolveFoldout = true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material material = materialEditor.target as Material;
        if (material == null)
            return;

        DrawSetupValidation(material);

        s_ProjectionFoldout = ShaderGUIHelper.GetFoldOut(
            s_ProjectionFoldout,
            "Projection",
            "Particle Decal box-volume projection settings");
        if (s_ProjectionFoldout)
        {
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Box Volume");
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "The projector mesh is a centered unit cube. Local +Z is the decal normal/forward, projection travels along local -Z, and local XY maps the decal UV.",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Angle Fade");
                EditorGUI.indentLevel++;
                MaterialProperty fadeStart = FindProperty("_AngleFadeStart", properties);
                MaterialProperty fadeEnd = FindProperty("_AngleFadeEnd", properties);
                materialEditor.ShaderProperty(fadeStart, "Angle Fade Start");
                materialEditor.ShaderProperty(fadeEnd, "Angle Fade End");

                if (!fadeStart.hasMixedValue && !fadeEnd.hasMixedValue && fadeEnd.floatValue <= fadeStart.floatValue)
                {
                    EditorGUILayout.HelpBox("Angle Fade End must be greater than Angle Fade Start.", MessageType.Warning);
                    if (GUILayout.Button("Use 60 / 90 Degree Fade"))
                    {
                        fadeStart.floatValue = 60.0f;
                        fadeEnd.floatValue = 90.0f;
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        s_RenderFoldout = ShaderGUIHelper.GetFoldOut(s_RenderFoldout, "Render Options", "Particle Decal render state");
        if (s_RenderFoldout)
        {
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Fixed Render State");
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(
                    "Transparent: SrcAlpha / OneMinusSrcAlpha, ZWrite Off, ZTest Always, Cull Front",
                    ShaderGUIHelper.Text);
                EditorGUI.indentLevel--;
            }

            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Stencil");
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_Stencil", properties), "Stencil ID");
                materialEditor.ShaderProperty(FindProperty("_StencilComp", properties), "Stencil Comparison");
                materialEditor.ShaderProperty(FindProperty("_StencilOp", properties), "Stencil Pass Operation");
                materialEditor.ShaderProperty(FindProperty("_StencilFailOp", properties), "Stencil Fail Operation");
                EditorGUI.indentLevel--;
            }
        }

        s_MainSettingsFoldout = ShaderGUIHelper.GetFoldOut(s_MainSettingsFoldout, "Main Settings", "Main color and emission");
        if (s_MainSettingsFoldout)
        {
            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Color");
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_MainColor", properties), "MainColor");
                EditorGUI.indentLevel--;
            }

            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Emission");
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_Glow", properties), "Emission Intensity");
                materialEditor.ShaderProperty(FindProperty("_GlowColor", properties), "Emission Color");
                EditorGUI.indentLevel--;
            }

            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                ShaderGUIHelper.GetSubHeader("Particle Data");
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(
                    FindProperty("_UseCustomData", properties),
                    "Use Custom1.x (Opacity / Dissolve)");
                EditorGUI.indentLevel--;
            }
        }

        s_MainTexFoldout = ShaderGUIHelper.GetFoldOut(s_MainTexFoldout, "MainColor", "Main texture settings");
        if (s_MainTexFoldout)
        {
            MaterialProperty mainTexture = FindProperty("_MainTex", properties);
            using (new EditorGUILayout.VerticalScope("helpbox"))
                DrawTexture(materialEditor, properties, "_MainTex", "BaseMap");

            if (mainTexture.textureValue != null)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    ShaderGUIHelper.GetSubHeader("MainTex Settings");
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(FindProperty("_ClampMainUV", properties), "ClampMainUV");
                    DrawUVAnimation(materialEditor, material, properties, "_MainTex", "_MAINTEX_ROTATOR_MOVE_MAINTEX");
                    EditorGUI.indentLevel--;
                }
            }
        }

        s_NoiseFoldout = ShaderGUIHelper.GetFoldOut(s_NoiseFoldout, "Noise", "Noise texture settings");
        if (s_NoiseFoldout)
        {
            MaterialProperty noiseTexture = FindProperty("_NoiseTex", properties);
            using (new EditorGUILayout.VerticalScope("helpbox"))
                DrawTexture(materialEditor, properties, "_NoiseTex", "NoiseTex");

            if (noiseTexture.textureValue != null)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    ShaderGUIHelper.GetSubHeader("NoiseTex Settings");
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(FindProperty("_NoiseTex_Channel", properties), "Channel");
                    DrawUVAnimation(materialEditor, material, properties, "_NoiseTex", "_NOISETEX_ROTATOR_MOVE_NOISETEX");
                    EditorGUI.indentLevel--;
                }
            }
        }

        s_MaskFoldout = ShaderGUIHelper.GetFoldOut(s_MaskFoldout, "Mask", "Mask texture settings");
        if (s_MaskFoldout)
        {
            MaterialProperty maskTexture = FindProperty("_MaskTex", properties);
            using (new EditorGUILayout.VerticalScope("helpbox"))
                DrawTexture(materialEditor, properties, "_MaskTex", "MaskTex");

            if (maskTexture.textureValue != null)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    ShaderGUIHelper.GetSubHeader("MaskTex Settings");
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(FindProperty("_MaskTex_Channel", properties), "Channel");
                    materialEditor.ShaderProperty(FindProperty("_MaskPower", properties), "Mask Power");
                    DrawUVAnimation(materialEditor, material, properties, "_MaskTex", "_MASKTEX_ROTATOR_MOVE_MASKTEX");
                    EditorGUI.indentLevel--;
                }

                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    ShaderGUIHelper.GetSubHeader("Noise");
                    EditorGUI.indentLevel++;
                    MaterialProperty useNoise = FindProperty("_NoiseMask", properties);
                    materialEditor.ShaderProperty(useNoise, "Use Noise");
                    using (new EditorGUI.DisabledScope(useNoise.floatValue < 0.5f))
                        materialEditor.ShaderProperty(FindProperty("_NoiseMaskInt", properties), "Noise Intensity");
                    EditorGUI.indentLevel--;
                }
            }
        }

        s_DistortionFoldout = ShaderGUIHelper.GetFoldOut(s_DistortionFoldout, "Distortion", "Distortion texture settings");
        if (s_DistortionFoldout)
        {
            MaterialProperty distortionTexture = FindProperty("_DistortionTex", properties);
            using (new EditorGUILayout.VerticalScope("helpbox"))
                DrawTexture(materialEditor, properties, "_DistortionTex", "DistortionTex");

            if (distortionTexture.textureValue != null)
            {
                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    ShaderGUIHelper.GetSubHeader("DistortionTex Settings");
                    EditorGUI.indentLevel++;
                    materialEditor.ShaderProperty(FindProperty("_DistortionTex_Channel", properties), "Channel");
                    materialEditor.ShaderProperty(FindProperty("_Distortion", properties), "Distortion Intensity");
                    DrawUVAnimation(materialEditor, material, properties, "_DistortionTex", "_DISTORTIONTEX_ROTATOR_MOVE_DISTORTIONTEX");
                    EditorGUI.indentLevel--;
                }

                using (new EditorGUILayout.VerticalScope("helpbox"))
                {
                    ShaderGUIHelper.GetSubHeader("Noise");
                    EditorGUI.indentLevel++;
                    MaterialProperty useNoise = FindProperty("_NoiseDistortion", properties);
                    materialEditor.ShaderProperty(useNoise, "Use Noise");
                    using (new EditorGUI.DisabledScope(useNoise.floatValue < 0.5f))
                        materialEditor.ShaderProperty(FindProperty("_NoiseDistortionInt", properties), "Noise Intensity");
                    EditorGUI.indentLevel--;
                }
            }
        }

        s_DissolveFoldout = ShaderGUIHelper.GetFoldOut(s_DissolveFoldout, "Dissolve", "Dissolve texture settings");
        if (s_DissolveFoldout)
        {
            MaterialProperty useDissolve = FindProperty("_UseDissolve", properties);
            materialEditor.ShaderProperty(useDissolve, "UseDissolve");
            if (useDissolve.floatValue > 0.5f)
            {
                MaterialProperty dissolveTexture = FindProperty("_DissolveTex", properties);
                using (new EditorGUILayout.VerticalScope("helpbox"))
                    DrawTexture(materialEditor, properties, "_DissolveTex", "DissolveTex");

                if (dissolveTexture.textureValue != null)
                {
                    using (new EditorGUILayout.VerticalScope("helpbox"))
                    {
                        ShaderGUIHelper.GetSubHeader("DissolveTex Settings");
                        EditorGUI.indentLevel++;
                        materialEditor.ShaderProperty(FindProperty("_DissolveTex_Channel", properties), "Channel");
                        MaterialProperty useCustomData = FindProperty("_UseCustomData", properties);
                        using (new EditorGUI.DisabledScope(useCustomData.floatValue > 0.5f))
                            materialEditor.ShaderProperty(FindProperty("_DissolveProgress", properties), "Dissolve Progress");
                        materialEditor.ShaderProperty(FindProperty("_DissolveAlphaControl", properties), "Dissolve Control Alpha");
                        materialEditor.ShaderProperty(FindProperty("_DissolveFlip", properties), "Dissolve Progress Flip");
                        DrawUVAnimation(materialEditor, material, properties, "_DissolveTex", "_DISSOLVETEX_ROTATOR_MOVE_DISSOLVETEX");
                        EditorGUI.indentLevel--;
                    }

                    using (new EditorGUILayout.VerticalScope("helpbox"))
                    {
                        ShaderGUIHelper.GetSubHeader("Dissolve Edge Settings");
                        EditorGUI.indentLevel++;
                        materialEditor.ShaderProperty(FindProperty("_DissolveColor", properties), "Dissolve Edge Color");
                        materialEditor.ShaderProperty(FindProperty("_DissolveRange", properties), "Dissolve Edge Range");
                        materialEditor.ShaderProperty(FindProperty("_DissolveMoveSmooth", properties), "Dissolve Edge Smoothness");
                        EditorGUI.indentLevel--;
                    }

                    using (new EditorGUILayout.VerticalScope("helpbox"))
                    {
                        ShaderGUIHelper.GetSubHeader("Dissolve Direction Settings");
                        EditorGUI.indentLevel++;
                        materialEditor.ShaderProperty(FindProperty("_DissolveControl", properties), "Dissolve Control");
                        materialEditor.ShaderProperty(FindProperty("_CenterDissolve", properties), "Center Dissolve");
                        materialEditor.ShaderProperty(FindProperty("_DissolveAxisDir", properties), "Dissolve Direction");
                        EditorGUI.indentLevel--;
                    }

                    using (new EditorGUILayout.VerticalScope("helpbox"))
                    {
                        ShaderGUIHelper.GetSubHeader("Noise");
                        EditorGUI.indentLevel++;
                        MaterialProperty useNoise = FindProperty("_NoiseDissolve", properties);
                        materialEditor.ShaderProperty(useNoise, "Use Noise");
                        using (new EditorGUI.DisabledScope(useNoise.floatValue < 0.5f))
                            materialEditor.ShaderProperty(FindProperty("_NoiseDissolveInt", properties), "Noise Intensity");
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }

        SynchronizeKeywords(materialEditor.targets);
        base.OnGUI(materialEditor, new MaterialProperty[] { });
    }

    private static void DrawTexture(
        MaterialEditor materialEditor,
        MaterialProperty[] properties,
        string propertyName,
        string label)
    {
        MaterialProperty texture = FindProperty(propertyName, properties);
        materialEditor.TexturePropertySingleLine(new GUIContent(label), texture);
        if (texture.textureValue != null)
        {
            EditorGUI.indentLevel++;
            materialEditor.TextureScaleOffsetProperty(texture);
            EditorGUI.indentLevel--;
        }
    }

    private static void DrawUVAnimation(
        MaterialEditor materialEditor,
        Material material,
        MaterialProperty[] properties,
        string prefix,
        string moveKeyword)
    {
        MaterialProperty moveCenterProperty = FindProperty(prefix + "_MoveCenter", properties);
        MaterialProperty animationProperty = FindProperty(prefix + "_Rotator", properties);
        MaterialProperty rotationProperty = FindProperty(prefix + "_Rota", properties);

        materialEditor.ShaderProperty(animationProperty, "UV Animation");
        Vector4 moveCenter = moveCenterProperty.vectorValue;
        if (material.IsKeywordEnabled(moveKeyword))
        {
            Vector2 move = EditorGUILayout.Vector2Field("Move Speed", new Vector2(moveCenter.x, moveCenter.y));
            moveCenter.x = move.x;
            moveCenter.y = move.y;
        }
        else
        {
            Vector2 center = EditorGUILayout.Vector2Field("RotateCenter", new Vector2(moveCenter.z, moveCenter.w));
            moveCenter.z = center.x;
            moveCenter.w = center.y;
            materialEditor.ShaderProperty(rotationProperty, "RotateSpeed");
        }

        moveCenterProperty.vectorValue = moveCenter;
    }

    private static void SynchronizeKeywords(Object[] targets)
    {
        foreach (Object target in targets)
        {
            if (target is not Material material)
                continue;

            SetKeyword(material, "_USEDISSOLVE", material.GetFloat("_UseDissolve") > 0.5f);
            SetKeyword(material, "_USECUSTOMDATA", material.GetFloat("_UseCustomData") > 0.5f);
        }
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }

    private static void DrawSetupValidation(Material material)
    {
        using (new EditorGUILayout.VerticalScope("helpbox"))
        {
            ShaderGUIHelper.GetSubHeader("Particle / Mesh Decal Requirements");
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Centered unit cube / local -Z points into receiver", ShaderGUIHelper.Text);
            EditorGUILayout.LabelField("MeshRenderer: object Transform defines projection volume", ShaderGUIHelper.Text);
            EditorGUILayout.LabelField("Particle System: Mesh mode / GPU instancing / required streams", ShaderGUIHelper.Text);
            EditorGUI.indentLevel--;

            UniversalRenderPipelineAsset pipelineAsset = UniversalRenderPipeline.asset;
            if (pipelineAsset != null && !pipelineAsset.supportsCameraDepthTexture)
            {
                EditorGUILayout.HelpBox(
                    "Decal projection requires Camera Depth Texture. Enable Depth Texture in the active URP Asset or on the Camera.",
                    MessageType.Warning);
            }

            List<ParticleSystemRenderer> particleRenderers = FindParticleRenderersUsing(material);
            List<MeshRenderer> meshRenderers = FindMeshRenderersUsing(material);
            if (particleRenderers.Count == 0 && meshRenderers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No active renderer using this material was found. Assign it to a centered unit-cube MeshRenderer or a Mesh particle system to validate its setup.",
                    MessageType.Info);
                return;
            }

            List<ParticleSystemRenderer> worldAlignedRenderers = particleRenderers
                .Where(renderer =>
                    renderer.alignment == ParticleSystemRenderSpace.World &&
                    !UsesStartRotation3D(renderer))
                .ToList();
            if (worldAlignedRenderers.Count > 0)
            {
                string worldNames = string.Join("\n", worldAlignedRenderers.Select(renderer => "- " + renderer.name));
                EditorGUILayout.HelpBox(
                    "Render Alignment = World does not inherit the emitter Transform rotation. " +
                    "The decal projects along each particle's final local -Z, so a Transform-aimed decal can be fully removed by Angle Fade. " +
                    "Use Local alignment, or explicitly aim World-aligned particles with Start Rotation 3D.\n" + worldNames,
                    MessageType.Warning);
            }

            List<ParticleSystemRenderer> invalidParticleRenderers = particleRenderers
                .Where(renderer => !IsValid(renderer))
                .ToList();
            List<MeshRenderer> invalidMeshRenderers = meshRenderers
                .Where(renderer => !IsValid(renderer))
                .ToList();
            if (invalidParticleRenderers.Count == 0 && invalidMeshRenderers.Count == 0)
            {
                EditorGUILayout.HelpBox("All active decal renderers using this material are configured correctly.", MessageType.Info);
                return;
            }

            if (invalidParticleRenderers.Count > 0)
            {
                string names = string.Join("\n", invalidParticleRenderers.Select(renderer => "- " + renderer.name));
                EditorGUILayout.HelpBox("These Particle System Renderers need repair:\n" + names, MessageType.Error);
                if (GUILayout.Button("Fix Particle Decal Renderers"))
                    FixParticleRenderers(invalidParticleRenderers);
            }

            if (invalidMeshRenderers.Count > 0)
            {
                string names = string.Join("\n", invalidMeshRenderers.Select(renderer => "- " + renderer.name));
                EditorGUILayout.HelpBox("These MeshRenderers need a centered unit cube:\n" + names, MessageType.Error);
                if (GUILayout.Button("Fix Mesh Decal Renderers"))
                    FixMeshRenderers(invalidMeshRenderers);
            }
        }
    }

    private static List<ParticleSystemRenderer> FindParticleRenderersUsing(Material material)
    {
        ParticleSystemRenderer[] allRenderers = Object.FindObjectsByType<ParticleSystemRenderer>();
        return allRenderers.Where(renderer => UsesMaterial(renderer, material)).ToList();
    }

    private static List<MeshRenderer> FindMeshRenderersUsing(Material material)
    {
        MeshRenderer[] allRenderers = Object.FindObjectsByType<MeshRenderer>();
        return allRenderers.Where(renderer => UsesMaterial(renderer, material)).ToList();
    }

    private static bool UsesMaterial(Renderer renderer, Material material)
    {
        return renderer.sharedMaterials.Contains(material);
    }

    private static bool IsValid(ParticleSystemRenderer renderer)
    {
        if (renderer.renderMode != ParticleSystemRenderMode.Mesh ||
            !HasValidProjectionAlignment(renderer) ||
            !renderer.enableGPUInstancing)
            return false;

        Mesh mesh = renderer.mesh;
        if (!IsUnitCube(mesh) || !renderer.supportsMeshInstancing)
            return false;

        List<ParticleSystemVertexStream> streams = new();
        renderer.GetActiveVertexStreams(streams);
        return streams.SequenceEqual(RequiredStreams);
    }

    private static bool IsValid(MeshRenderer renderer)
    {
        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        return meshFilter != null && IsUnitCube(meshFilter.sharedMesh);
    }

    private static bool HasValidProjectionAlignment(ParticleSystemRenderer renderer)
    {
        if (renderer.alignment == ParticleSystemRenderSpace.Local)
            return true;

        return renderer.alignment == ParticleSystemRenderSpace.World && UsesStartRotation3D(renderer);
    }

    private static bool UsesStartRotation3D(ParticleSystemRenderer renderer)
    {
        ParticleSystem particleSystem = renderer.GetComponent<ParticleSystem>();
        return particleSystem != null && particleSystem.main.startRotation3D;
    }

    private static bool IsUnitCube(Mesh mesh)
    {
        if (mesh == null)
            return false;

        Bounds bounds = mesh.bounds;
        return bounds.center.sqrMagnitude < 0.0001f && (bounds.size - Vector3.one).sqrMagnitude < 0.0001f;
    }

    private static void FixParticleRenderers(IEnumerable<ParticleSystemRenderer> renderers)
    {
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        foreach (ParticleSystemRenderer renderer in renderers)
        {
            Undo.RecordObject(renderer, "Fix Particle Decal Renderer");
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.mesh = cube;
            renderer.enableGPUInstancing = true;
            renderer.SetActiveVertexStreams(RequiredStreams);
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void FixMeshRenderers(IEnumerable<MeshRenderer> renderers)
    {
        Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        foreach (MeshRenderer renderer in renderers)
        {
            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = Undo.AddComponent<MeshFilter>(renderer.gameObject);

            Undo.RecordObject(meshFilter, "Fix Mesh Decal Renderer");
            meshFilter.sharedMesh = cube;
            EditorUtility.SetDirty(meshFilter);
        }
    }
}
