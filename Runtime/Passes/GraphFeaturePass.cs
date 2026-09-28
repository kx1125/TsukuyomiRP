namespace Tsukuyomi.Rendering
{
    /// <summary>Records zero or more native nodes at one injection point, without an open builder.</summary>
    [System.Serializable]
    public abstract class GraphFeaturePass : RenderPassBase
    {
        public abstract void RecordGraph(in FeatureGraphContext context);
    }
}
