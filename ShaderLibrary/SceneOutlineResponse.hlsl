#ifndef TSUKUYOMI_SCENE_OUTLINE_RESPONSE_INCLUDED
#define TSUKUYOMI_SCENE_OUTLINE_RESPONSE_INCLUDED
float SceneOutlineScanStrength(float2 delta, float eyeDepth)
{
    return saturate(max(delta.y * 1.5, delta.x / max(.05, eyeDepth * .03)));
}
float SceneOutlineBaseCoverage(float2 delta, float eyeDepth, float2 thresholds)
{
    float edge = smoothstep(thresholds.x, thresholds.x * 1.5, delta.x / max(.05, eyeDepth));
    if (thresholds.y >= 0) edge = max(edge, smoothstep(thresholds.y, thresholds.y * 1.5, delta.y));
    return edge;
}
#endif
