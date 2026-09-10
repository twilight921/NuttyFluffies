using UnityEngine;
using UnityEngine.Splines;

// Arc-length reparameterization of a spline: lets callers walk a track by real
// distance (world units) instead of the spline's non-uniform t. Pulled out of
// BuildCoasterTrain so the runtime LevelLoader spaces the train exactly the
// way the editor train-builder does.
//
// NOTE: SplineContainer.EvaluatePosition/EvaluateTangent return WORLD space in
// this project's Splines package version -- do NOT also run the results
// through transform.TransformPoint.
public sealed class SplineArcLengthTable
{
    public readonly Vector3[] WorldPositions;
    public readonly float[] TSamples;
    public readonly float[] CumulativeDistance;
    public readonly float TotalLength;

    public SplineArcLengthTable(SplineContainer container, int sampleCount = 800)
    {
        sampleCount = Mathf.Max(2, sampleCount);
        WorldPositions = new Vector3[sampleCount];
        TSamples = new float[sampleCount];
        CumulativeDistance = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / (sampleCount - 1);
            Vector3 world = container.EvaluatePosition(t);
            WorldPositions[i] = world;
            TSamples[i] = t;
            CumulativeDistance[i] = i == 0
                ? 0f
                : CumulativeDistance[i - 1] + Vector3.Distance(WorldPositions[i - 1], world);
        }

        TotalLength = CumulativeDistance[sampleCount - 1];
    }

    // Normalized spline t at a given arc distance from the start (clamped).
    public float TAtDistance(float distance)
    {
        distance = Mathf.Clamp(distance, 0f, TotalLength);
        int lo = 0, hi = WorldPositions.Length - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (CumulativeDistance[mid] < distance) lo = mid; else hi = mid;
        }
        float segLen = CumulativeDistance[hi] - CumulativeDistance[lo];
        float segT = segLen > 0.0001f ? (distance - CumulativeDistance[lo]) / segLen : 0f;
        return Mathf.Lerp(TSamples[lo], TSamples[hi], segT);
    }

    // Index of the sample nearest a world position (brute force -- reliable on
    // long, multi-hump tracks where SplineUtility.GetNearestPoint's default
    // resolution is not).
    public int NearestSampleIndex(Vector3 worldPos, out float distance)
    {
        int best = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < WorldPositions.Length; i++)
        {
            float sqr = (WorldPositions[i] - worldPos).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = i; }
        }
        distance = Mathf.Sqrt(bestSqr);
        return best;
    }
}
