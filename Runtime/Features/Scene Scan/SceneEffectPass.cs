using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    /// <summary>Owns only requested scene effects and their common mask/composition intermediates.</summary>
    internal sealed class SceneEffectPass : RenderPassBase, IRenderPassService
    {
        private VoxelTransitionPass _openingSettings;
        private ScanPostProcessPass _postSettings;
        private XrayRenderPass _xraySettings;
        private SpatialGridRenderPass _gridSettings;
        private TsukuyomiVoxelTransitionPass _opening;
        private TsukuyomiScanPostProcessPass _post;
        private TsukuyomiXrayRenderPass _mask;
        private TsukuyomiSpatialGridRenderPass _grid;
        private ScanColorCompositePass _composite;
        private XrayContributionNode _xray;
        private ScanVoxelGlitchNode _voxel;
        private ScanChannelGlitchNode _channel;
        internal const string Key = "tsukuyomi.scene-effects";
        public override string Name => "Scene Effect Composition";
        internal void Register(RenderPassBase definition)
        {
            switch (definition)
            {
                case VoxelTransitionPass p: _openingSettings = p; break;
                case ScanPostProcessPass p: _postSettings = p; break;
                case XrayRenderPass p: _xraySettings = p; break;
                case SpatialGridRenderPass p: _gridSettings = p; break;
            }
        }
        public override void Build(PassPlanBuilder plan)
        {
            if (_openingSettings != null) _opening = new TsukuyomiVoxelTransitionPass(_openingSettings);
            if (_postSettings != null) _post = new TsukuyomiScanPostProcessPass(_postSettings);
            if (_gridSettings != null) _grid = new TsukuyomiSpatialGridRenderPass(_gridSettings);
            _mask = new TsukuyomiXrayRenderPass(_xraySettings, () => _composite.Prepared || _grid?.Prepared == true) { HighlightAllowed = _xraySettings != null };
            _composite = new ScanColorCompositePass(_opening, _post, _xraySettings != null ? _mask : null, ownsPost: true);
            var stage = RenderPassEvent.BeforeRenderingTransparents - 1;
            var mask = plan.Add(_mask, stage, () => _mask.Prepared);
            PassPlanNode opening = null, xray = null;
            if (_opening != null) { opening = plan.Add(_opening, stage, () => _opening.Prepared); plan.After(opening, mask, true); }
            if (_xraySettings != null)
            {
                _xray = new XrayContributionNode(_mask);
                xray = plan.Add(_xray, stage, () => _xray.Prepared); plan.After(xray, mask);
                if (opening != null) plan.After(xray, opening, true);
            }
            var composite = plan.Add(_composite, stage, () => _composite.Prepared);
            plan.After(composite, mask, true);
            if (opening != null) plan.After(composite, opening, true);
            if (xray != null) plan.After(composite, xray, true);
            if (_grid != null)
            {
                var grid = plan.Add(_grid, stage, () => _grid.Prepared);
                plan.After(grid, composite, true); plan.After(grid, mask, true);
            }
            if (_post != null)
            {
                _voxel = new ScanVoxelGlitchNode(_post); _channel = new ScanChannelGlitchNode(_post);
                var voxel = plan.Add(_voxel, RenderPassEvent.BeforeRenderingPostProcessing - 2, () => _voxel.Prepared);
                var channel = plan.Add(_channel, RenderPassEvent.BeforeRenderingPostProcessing - 2, () => _channel.Prepared);
                plan.After(voxel, composite, true); plan.After(channel, voxel, true);
            }
        }
    }
}
