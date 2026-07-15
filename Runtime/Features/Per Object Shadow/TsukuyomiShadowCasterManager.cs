using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiShadowCasterManager
    {
        private const int FrustumCornerCount = 8;

        private static readonly HashSet<ITsukuyomiShadowCaster> s_Casters = new();
        private static readonly int[] s_FrustumTriangleIndices =
        {
            0, 3, 1,
            1, 3, 2,
            2, 3, 7,
            2, 7, 6,
            0, 5, 4,
            0, 1, 5,
            1, 2, 5,
            2, 6, 5,
            0, 7, 3,
            0, 4, 7,
            4, 7, 5,
            5, 7, 6,
        };
        private static readonly Matrix4x4 s_FlipZMatrix = Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f));
        private static readonly Vector3[] s_FrustumCornerBuffer = new Vector3[4];
        private static int s_NextCasterId = 1;

        private readonly List<int> _rendererIndexList = new();
        private readonly List<CullingResult> _cullingResults = new();
        private readonly Vector4[] _frustumCorners = new Vector4[FrustumCornerCount];
        private readonly List<Vector3> _clipInput = new(8);
        private readonly List<Vector3> _clipOutput = new(8);

        public int VisibleCount => _cullingResults.Count;

        public static void Register(ITsukuyomiShadowCaster caster)
        {
            if (caster == null)
                return;

            if (s_Casters.Add(caster))
                caster.Id = s_NextCasterId++;
        }

        public static void Unregister(ITsukuyomiShadowCaster caster)
        {
            if (caster != null)
                s_Casters.Remove(caster);
        }

        public int GetId(int index)
        {
            return _cullingResults[index].Caster.Id;
        }

        public void GetMatrices(int index, out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix)
        {
            CullingResult result = _cullingResults[index];
            viewMatrix = result.ViewMatrix;
            projectionMatrix = result.ProjectionMatrix;
        }

        public void Draw(RasterCommandBuffer cmd, int index)
        {
            CullingResult result = _cullingResults[index];
            for (int i = result.RendererIndexStartInclusive; i < result.RendererIndexEndExclusive; i++)
                result.Caster.RendererList.Draw(cmd, _rendererIndexList[i]);
        }

        public void Cull(UniversalCameraData cameraData, UniversalLightData lightData, int maxCount, float shadowLengthOffset)
        {
            _rendererIndexList.Clear();
            _cullingResults.Clear();

            if (s_Casters.Count == 0 || !TryGetMainLight(lightData, out VisibleLight mainLight))
                return;

            Camera camera = cameraData.camera;
            SetFrustumEightCorners(_frustumCorners, camera);

            Matrix4x4 mainLightLocalToWorld = mainLight.localToWorldMatrix;
            Transform cameraTransform = camera.transform;

            foreach (ITsukuyomiShadowCaster caster in s_Casters)
                CullAndAppend(caster, cameraTransform, mainLightLocalToWorld, shadowLengthOffset);

            _cullingResults.Sort((lhs, rhs) => lhs.Priority.CompareTo(rhs.Priority));

            if (_cullingResults.Count > maxCount)
                _cullingResults.RemoveRange(maxCount, _cullingResults.Count - maxCount);
        }

        private void CullAndAppend(
            ITsukuyomiShadowCaster caster,
            Transform cameraTransform,
            Matrix4x4 mainLightLocalToWorld,
            float shadowLengthOffset)
        {
            if (!caster.CanCastShadow())
                return;

            int rendererIndexStart = _rendererIndexList.Count;
            if (!caster.RendererList.TryGetWorldBounds(out Bounds bounds, _rendererIndexList))
                return;

            Vector3 aabbCenter = bounds.center;
            Quaternion lightRotation = mainLightLocalToWorld.rotation;
            Matrix4x4 viewMatrix = Matrix4x4.TRS(aabbCenter, lightRotation, Vector3.one).inverse;
            viewMatrix = s_FlipZMatrix * viewMatrix;

            if (!TryGetProjectionMatrix(viewMatrix, bounds, shadowLengthOffset, out Matrix4x4 projectionMatrix))
            {
                _rendererIndexList.RemoveRange(rendererIndexStart, _rendererIndexList.Count - rendererIndexStart);
                return;
            }

            Vector3 toCaster = aabbCenter - cameraTransform.position;
            float distancePriority = Mathf.Clamp01(toCaster.sqrMagnitude / 10000.0f);
            float cosAngle = Vector3.Dot(cameraTransform.forward, toCaster.normalized);
            float priority = distancePriority + (-cosAngle * 0.5f + 0.5f) * 100.0f + caster.RendererList.Priority;
            caster.Priority = priority;

            _cullingResults.Add(new CullingResult
            {
                Caster = caster,
                RendererIndexStartInclusive = rendererIndexStart,
                RendererIndexEndExclusive = _rendererIndexList.Count,
                ViewMatrix = viewMatrix,
                ProjectionMatrix = projectionMatrix,
                Priority = priority
            });
        }

        private bool TryGetProjectionMatrix(Matrix4x4 viewMatrix, Bounds bounds, float shadowLengthOffset, out Matrix4x4 projectionMatrix)
        {
            GetViewSpaceShadowAABB(viewMatrix, bounds, shadowLengthOffset, out Vector3 shadowMin, out Vector3 shadowMax);

            if (!AdjustViewSpaceShadowAABB(viewMatrix, ref shadowMin, ref shadowMax))
            {
                projectionMatrix = default;
                return false;
            }

            float width = shadowMax.x * 2.0f;
            float height = shadowMax.y * 2.0f;
            float zNear = -shadowMax.z;
            float zFar = -shadowMin.z;
            projectionMatrix = Matrix4x4.Ortho(-width * 0.5f, width * 0.5f, -height * 0.5f, height * 0.5f, zNear, zFar);
            return true;
        }

        private static void GetViewSpaceShadowAABB(Matrix4x4 viewMatrix, Bounds bounds, float shadowLengthOffset, out Vector3 shadowMin, out Vector3 shadowMax)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            shadowMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            shadowMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            EncapsulateViewSpacePoint(viewMatrix, new Vector3(min.x, min.y, min.z), ref shadowMin, ref shadowMax);
            EncapsulateViewSpacePoint(viewMatrix, new Vector3(max.x, min.y, min.z), ref shadowMin, ref shadowMax);
            EncapsulateViewSpacePoint(viewMatrix, new Vector3(min.x, max.y, min.z), ref shadowMin, ref shadowMax);
            EncapsulateViewSpacePoint(viewMatrix, new Vector3(min.x, min.y, max.z), ref shadowMin, ref shadowMax);
            EncapsulateViewSpacePoint(viewMatrix, new Vector3(max.x, max.y, min.z), ref shadowMin, ref shadowMax);
            EncapsulateViewSpacePoint(viewMatrix, new Vector3(max.x, min.y, max.z), ref shadowMin, ref shadowMax);
            EncapsulateViewSpacePoint(viewMatrix, new Vector3(min.x, max.y, max.z), ref shadowMin, ref shadowMax);
            EncapsulateViewSpacePoint(viewMatrix, new Vector3(max.x, max.y, max.z), ref shadowMin, ref shadowMax);

            shadowMin.z = Mathf.Min(shadowMin.z, shadowMax.z - shadowLengthOffset);
        }

        private bool AdjustViewSpaceShadowAABB(Matrix4x4 viewMatrix, ref Vector3 shadowMin, ref Vector3 shadowMax)
        {
            bool isVisibleXY = false;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            for (int i = 0; i < s_FrustumTriangleIndices.Length; i += 3)
            {
                _clipInput.Clear();
                _clipInput.Add(viewMatrix.MultiplyPoint3x4(_frustumCorners[s_FrustumTriangleIndices[i + 0]]));
                _clipInput.Add(viewMatrix.MultiplyPoint3x4(_frustumCorners[s_FrustumTriangleIndices[i + 1]]));
                _clipInput.Add(viewMatrix.MultiplyPoint3x4(_frustumCorners[s_FrustumTriangleIndices[i + 2]]));

                ClipPolygon(0, shadowMin.x, true);
                ClipPolygon(0, shadowMax.x, false);
                ClipPolygon(1, shadowMin.y, true);
                ClipPolygon(1, shadowMax.y, false);

                if (_clipInput.Count < 3)
                    continue;

                isVisibleXY = true;
                for (int j = 0; j < _clipInput.Count; j++)
                {
                    minZ = Mathf.Min(minZ, _clipInput[j].z);
                    maxZ = Mathf.Max(maxZ, _clipInput[j].z);
                }
            }

            if (isVisibleXY && minZ < shadowMax.z && maxZ > shadowMin.z)
            {
                shadowMin.z = Mathf.Max(shadowMin.z, minZ);
                return true;
            }

            return false;
        }

        private void ClipPolygon(int componentIndex, float edgeValue, bool isMinEdge)
        {
            _clipOutput.Clear();
            if (_clipInput.Count == 0)
                return;

            Vector3 previous = _clipInput[^1];
            bool previousInside = IsInsideEdge(previous, componentIndex, edgeValue, isMinEdge);

            for (int i = 0; i < _clipInput.Count; i++)
            {
                Vector3 current = _clipInput[i];
                bool currentInside = IsInsideEdge(current, componentIndex, edgeValue, isMinEdge);

                if (currentInside != previousInside)
                    _clipOutput.Add(IntersectEdge(previous, current, componentIndex, edgeValue));

                if (currentInside)
                    _clipOutput.Add(current);

                previous = current;
                previousInside = currentInside;
            }

            _clipInput.Clear();
            _clipInput.AddRange(_clipOutput);
        }

        private static bool IsInsideEdge(Vector3 point, int componentIndex, float edgeValue, bool isMinEdge)
        {
            float value = componentIndex == 0 ? point.x : point.y;
            return isMinEdge ? value > edgeValue : value < edgeValue;
        }

        private static Vector3 IntersectEdge(Vector3 a, Vector3 b, int componentIndex, float edgeValue)
        {
            float aValue = componentIndex == 0 ? a.x : a.y;
            float bValue = componentIndex == 0 ? b.x : b.y;
            float t = Mathf.Approximately(aValue, bValue) ? 0.0f : (edgeValue - aValue) / (bValue - aValue);
            return Vector3.LerpUnclamped(a, b, t);
        }

        private static void EncapsulateViewSpacePoint(Matrix4x4 viewMatrix, Vector3 pointWS, ref Vector3 shadowMin, ref Vector3 shadowMax)
        {
            Vector3 point = viewMatrix.MultiplyPoint3x4(pointWS);
            shadowMin = Vector3.Min(shadowMin, point);
            shadowMax = Vector3.Max(shadowMax, point);
        }

        private static bool TryGetMainLight(UniversalLightData lightData, out VisibleLight mainLight)
        {
            int mainLightIndex = lightData.mainLightIndex;
            if (mainLightIndex < 0 || mainLightIndex >= lightData.visibleLights.Length)
            {
                mainLight = default;
                return false;
            }

            mainLight = lightData.visibleLights[mainLightIndex];
            return mainLight.lightType == LightType.Directional;
        }

        private static void SetFrustumEightCorners(Vector4[] frustumEightCorners, Camera camera)
        {
            Transform transform = camera.transform;
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;

            if (camera.orthographic)
            {
                float top = camera.orthographicSize;
                float right = top * camera.aspect;

                frustumEightCorners[0] = TransformPoint(transform, -right, -top, near);
                frustumEightCorners[1] = TransformPoint(transform, -right, top, near);
                frustumEightCorners[2] = TransformPoint(transform, right, top, near);
                frustumEightCorners[3] = TransformPoint(transform, right, -top, near);
                frustumEightCorners[4] = TransformPoint(transform, -right, -top, far);
                frustumEightCorners[5] = TransformPoint(transform, -right, top, far);
                frustumEightCorners[6] = TransformPoint(transform, right, top, far);
                frustumEightCorners[7] = TransformPoint(transform, right, -top, far);
                return;
            }

            Rect viewport = new(0, 0, 1, 1);
            const Camera.MonoOrStereoscopicEye eye = Camera.MonoOrStereoscopicEye.Mono;

            camera.CalculateFrustumCorners(viewport, near, eye, s_FrustumCornerBuffer);
            for (int i = 0; i < 4; i++)
                frustumEightCorners[i] = TransformPoint(transform, s_FrustumCornerBuffer[i]);

            camera.CalculateFrustumCorners(viewport, far, eye, s_FrustumCornerBuffer);
            for (int i = 0; i < 4; i++)
                frustumEightCorners[i + 4] = TransformPoint(transform, s_FrustumCornerBuffer[i]);
        }

        private static Vector4 TransformPoint(Transform transform, float x, float y, float z)
        {
            return TransformPoint(transform, new Vector3(x, y, z));
        }

        private static Vector4 TransformPoint(Transform transform, Vector3 point)
        {
            Vector3 world = transform.TransformPoint(point);
            return new Vector4(world.x, world.y, world.z, 1.0f);
        }

        private struct CullingResult
        {
            public ITsukuyomiShadowCaster Caster;
            public int RendererIndexStartInclusive;
            public int RendererIndexEndExclusive;
            public Matrix4x4 ViewMatrix;
            public Matrix4x4 ProjectionMatrix;
            public float Priority;
        }
    }
}
