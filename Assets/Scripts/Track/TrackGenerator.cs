using System.Collections.Generic;
using UnityEngine;

// Deterministic procedural coaster layout: a LevelDefinition's seed + shape
// params -> an ordered list of spline knot positions (container-local space, X
// strictly increasing). Same inputs always produce the same track, so a
// LevelDefinition asset fully captures a level without storing every knot
// (unless the designer bakes + hand-tunes -- see LevelDefinition.useBakedKnots).
//
// Playability rules baked in:
//   * a flat lead-in  (train spawn + settle) and a flat run-out (clean finish
//     before RunEndTrigger fires),
//   * X evenly spaced and strictly increasing,
//   * an ease-in from the flat lead-in so there's no corner where hills start,
//   * a slope-clamp post-pass so no single segment is an unrideable wall.
//
// Pure C# (no UnityEditor), so LevelLoader can call it at runtime in a build.
public static class TrackGenerator
{
    // Max |dy/dx| allowed for any one segment after generation. ~67deg.
    private const float MaxSegmentSlope = 2.4f;

    public static List<Vector3> Generate(LevelDefinition def)
    {
        var knots = new List<Vector3>();
        if (def == null) return knots;

        float length = Mathf.Max(def.length, 24f);
        float spacing = Mathf.Clamp(def.knotSpacing, 1f, length / 3f);
        int count = Mathf.Max(4, Mathf.RoundToInt(length / spacing) + 1);

        var rng = new System.Random(def.seed);
        float phaseA = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float phaseB = (float)(rng.NextDouble() * Mathf.PI * 2.0);
        float freqJitter = 0.75f + (float)rng.NextDouble() * 0.5f; // 0.75..1.25

        float leadIn = Mathf.Clamp(def.leadInLength, 0f, length * 0.35f);
        float runOut = Mathf.Clamp(def.runOutLength, 0f, length * 0.35f);
        float hillStart = leadIn;
        float hillEnd = length - runOut;

        float freq = Mathf.Max(0.03f, def.hillFrequency) * freqJitter;
        float roughness = Mathf.Clamp01(def.roughness);
        float lastHillY = 0f;

        for (int i = 0; i < count; i++)
        {
            float x = length * i / (count - 1);
            float y;

            if (x <= hillStart)
            {
                y = 0f; // flat spawn
            }
            else if (x >= hillEnd)
            {
                y = lastHillY; // hold the finish flat at whatever height the hills left off
            }
            else
            {
                float progress = Mathf.InverseLerp(hillStart, hillEnd, x);
                float envelope = def.amplitudeEnvelope != null
                    ? Mathf.Clamp01(def.amplitudeEnvelope.Evaluate(progress))
                    : 1f;

                // Ease in over ~one knot span so the first hill grows out of the
                // flat lead-in instead of kinking off it.
                float easeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - hillStart) / Mathf.Max(spacing, 0.01f)));

                float main = Mathf.Sin(x * freq + phaseA);
                float fine = Mathf.Sin(x * freq * 3.3f + phaseB) * roughness;
                float hill = (main + fine) / (1f + roughness) * def.maxHillHeight * envelope * easeIn;

                float bias = -def.downhillBias * (x - hillStart); // overall forward-favoring drop

                y = hill + bias;
                lastHillY = y;
            }

            knots.Add(new Vector3(x, y, 0f));
        }

        ClampSlopes(knots, MaxSegmentSlope);
        return knots;
    }

    // Forward then backward pass capping |dy/dx| per segment so nothing is a
    // near-vertical wall the cart can't climb or safely drop. The flat lead-in
    // (all zeros) and the held-flat run-out are already within limits, so they
    // pass through untouched.
    private static void ClampSlopes(List<Vector3> knots, float maxSlope)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            bool forward = pass == 0;
            int start = forward ? 1 : knots.Count - 2;
            int end = forward ? knots.Count : -1;
            int step = forward ? 1 : -1;

            for (int i = start; i != end; i += step)
            {
                int prev = forward ? i - 1 : i + 1;
                float dx = Mathf.Abs(knots[i].x - knots[prev].x);
                float maxDy = maxSlope * dx;
                float dy = knots[i].y - knots[prev].y;
                if (Mathf.Abs(dy) > maxDy)
                {
                    Vector3 k = knots[i];
                    k.y = knots[prev].y + Mathf.Sign(dy) * maxDy;
                    knots[i] = k;
                }
            }
        }
    }
}
