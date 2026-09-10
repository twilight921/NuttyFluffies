using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

// Shared, runtime-safe track construction: writes a knot list onto a
// SplineContainer's primary spline, bakes a matching EdgeCollider2D rail, and
// redraws the LineRenderer to follow it. Both the editor tool (LevelBuilder)
// and the runtime loader (LevelLoader) call this, so the track a designer
// previews in-editor is the exact track the player rides -- collider AND
// visual.
//
// The EdgeCollider2D is the physical rail -- CoasterCart is fully
// physics-driven, so without a collider baked to follow the new spline the
// cart just falls. The LineRenderer is the only thing the player actually
// sees; nothing else in the scene re-samples it (there's no SplineLineSampler
// component), so it has to be rebaked here too or it keeps drawing the old
// hand-authored curve.
public static class SplineTrackBuilder
{
    public const int DefaultColliderSamples = 600;
    public const int DefaultLineSamples = 256;
    public const float DefaultEdgeRadius = 0.12f;

    // `knots` are in the container's local space (the space shown in the Spline
    // inspector). Tangents are auto-smoothed, matching the Mode 0 / Tension 0.5
    // the original hand-authored track used.
    public static void Build(SplineContainer container, EdgeCollider2D edge, LineRenderer line, IList<Vector3> knots,
        int colliderSamples = DefaultColliderSamples, int lineSamples = DefaultLineSamples, float edgeRadius = DefaultEdgeRadius)
    {
        if (container == null || knots == null || knots.Count < 2)
        {
            Debug.LogError("[SplineTrackBuilder] Need a SplineContainer and at least 2 knots.");
            return;
        }

        if (container.Splines.Count == 0)
            container.AddSpline();

        Spline spline = container.Spline;
        spline.Clear();
        for (int i = 0; i < knots.Count; i++)
        {
            Vector3 k = knots[i];
            spline.Add(new BezierKnot(new float3(k.x, k.y, k.z)), TangentMode.AutoSmooth);
        }

        if (edge != null)
            BakeCollider(container, edge, colliderSamples, edgeRadius);
        if (line != null)
            BakeLine(container, line, lineSamples);
    }

    // Samples the finished spline in world space, converts each point into the
    // collider transform's local space (EdgeCollider2D points are always
    // local), and writes them as the rail polyline.
    public static void BakeCollider(SplineContainer container, EdgeCollider2D edge,
        int samples = DefaultColliderSamples, float edgeRadius = DefaultEdgeRadius)
    {
        if (container == null || edge == null) return;
        samples = Mathf.Max(2, samples);

        var pts = new List<Vector2>(samples);
        Transform t = edge.transform;
        for (int i = 0; i < samples; i++)
        {
            float u = (float)i / (samples - 1);
            Vector3 world = container.EvaluatePosition(u);
            Vector3 local = t.InverseTransformPoint(world);
            pts.Add(new Vector2(local.x, local.y));
        }

        edge.edgeRadius = edgeRadius;
        edge.points = pts.ToArray();
    }

    // Redraws the LineRenderer along the finished spline. SplineContainer
    // .EvaluatePosition returns WORLD space; honor the renderer's own
    // useWorldSpace so the line lands in the right place either way.
    public static void BakeLine(SplineContainer container, LineRenderer line, int samples = DefaultLineSamples)
    {
        if (container == null || line == null) return;
        samples = Mathf.Max(2, samples);

        var positions = new Vector3[samples];
        Transform t = line.transform;
        for (int i = 0; i < samples; i++)
        {
            float u = (float)i / (samples - 1);
            Vector3 world = container.EvaluatePosition(u);
            positions[i] = line.useWorldSpace ? world : t.InverseTransformPoint(world);
        }

        line.positionCount = samples;
        line.SetPositions(positions);
    }
}
