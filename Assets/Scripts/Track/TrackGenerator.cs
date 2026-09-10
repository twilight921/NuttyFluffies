using System.Collections.Generic;
using UnityEngine;

// Deterministic procedural coaster layout: a LevelDefinition's seed + shape
// params -> an ordered list of spline knot positions (container-local space, X
// strictly increasing). Same inputs always produce the same track, so a
// LevelDefinition asset fully captures a level without storing every knot
// (unless the designer bakes + hand-tunes -- see LevelDefinition.useBakedKnots).
//
// Two layers of shape:
//   1. Base rolling hills -- a sine swell whose waveform is warped per
//      LevelDefinition.archetype so a "Switchback" and a "Plunges" level don't
//      start from the same curve.
//   2. Signature features -- discrete hairpins / launch crests / plunges /
//      washboard runs scattered on top (counts + intensity from the asset).
//      Each feature type leans on a different premium creature's Heart action,
//      so every level has a reason to swap passengers.
//
// Playability rules baked in:
//   * a flat lead-in  (train spawn + settle) and a flat run-out (clean finish
//     before RunEndTrigger fires) -- features never touch those spans,
//   * X evenly spaced and strictly increasing,
//   * an ease-in from the flat lead-in so there's no corner where hills start,
//   * a slope-clamp post-pass so no single segment is an unrideable wall --
//     this also self-limits an over-eager feature to the steepest rideable shape.
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
        // Plunges lean the whole track forward; Switchbacks stay flatter overall
        // so the turns read rather than the drop.
        float biasScale = def.archetype == TrackArchetype.Plunges ? 1.35f
            : def.archetype == TrackArchetype.Switchback ? 0.6f
            : 1f;
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

                float main = ShapeWave(x * freq + phaseA, def.archetype);
                float fine = Mathf.Sin(x * freq * 3.3f + phaseB) * roughness;
                float hill = (main + fine) / (1f + roughness) * def.maxHillHeight * envelope * easeIn;

                float bias = -def.downhillBias * biasScale * (x - hillStart); // overall forward-favoring drop

                y = hill + bias;
                lastHillY = y;
            }

            knots.Add(new Vector3(x, y, 0f));
        }

        ApplyFeatures(knots, def, rng, hillStart, hillEnd, spacing);
        ClampSlopes(knots, MaxSegmentSlope);
        return knots;
    }

    // Warps the -1..1 base sine into an archetype-specific waveform so the
    // underlying hills already feel different before any features land. Output
    // stays within roughly [-1.2, 1.2].
    private static float ShapeWave(float phase, TrackArchetype archetype)
    {
        float s = Mathf.Sin(phase);
        float shaped;
        switch (archetype)
        {
            case TrackArchetype.Switchback:
                // Flatten the swells, sharpen the reversals -> quick S-bends.
                shaped = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.55f);
                break;
            case TrackArchetype.Airborne:
                // Pointy crests, broad shallow valleys -> abrupt lips to launch off.
                shaped = s > 0f ? Mathf.Pow(s, 0.5f) : -Mathf.Pow(-s, 1.8f);
                break;
            case TrackArchetype.Plunges:
                // Phase-warp: lazy climb, sudden fall.
                shaped = Mathf.Sin(phase + 0.7f * Mathf.Sin(phase));
                break;
            case TrackArchetype.Whoops:
                // Add a hard third harmonic for a choppier ride.
                shaped = (s + 0.45f * Mathf.Sin(phase * 3f)) / 1.45f;
                break;
            case TrackArchetype.Mixed:
                shaped = 0.5f * (Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.6f))
                    + 0.5f * ((s + 0.4f * Mathf.Sin(phase * 3f)) / 1.4f);
                break;
            default:
                shaped = s;
                break;
        }
        return Mathf.Clamp(shaped, -1.2f, 1.2f);
    }

    // Scatters the asset's signature features across the hill section only (the
    // flat lead-in / run-out are left alone). Feature order is fixed and the rng
    // stream is shared with Generate, so the result stays fully deterministic.
    private static void ApplyFeatures(List<Vector3> knots, LevelDefinition def, System.Random rng,
        float hillStart, float hillEnd, float spacing)
    {
        int first = 0;
        while (first < knots.Count && knots[first].x <= hillStart) first++;
        int last = knots.Count - 1;
        while (last > 0 && knots[last].x >= hillEnd) last--;
        // Need room for a feature window plus its neighbours.
        if (last - first < 6) return;

        PlaceFeatures(knots, first, last, Mathf.Clamp(def.hairpinCount, 0, 8), rng,
            (k, a) => Hairpin(k, a, def.hairpinSharpness, spacing));
        PlaceFeatures(knots, first, last, Mathf.Clamp(def.launchCount, 0, 8), rng,
            (k, a) => Launch(k, a, def.launchStrength, spacing));
        PlaceFeatures(knots, first, last, Mathf.Clamp(def.plungeCount, 0, 6), rng,
            (k, a) => Plunge(k, a, def.plungeSteepness, spacing));
        PlaceFeatures(knots, first, last, Mathf.Clamp(def.washboardCount, 0, 8), rng,
            (k, a) => Washboard(k, a, def.washboardStrength, spacing));
    }

    // Spreads `count` feature anchors evenly across [first, last] with a small
    // deterministic jitter, then invokes `apply` at each anchor index.
    private static void PlaceFeatures(List<Vector3> knots, int first, int last, int count,
        System.Random rng, System.Action<List<Vector3>, int> apply)
    {
        if (count <= 0) return;

        // Keep a feature's reach (up to 2 knots either side) inside the hill
        // section so it never eats into the flat lead-in or run-out.
        int lo = first + 2;
        int hi = last - 2;
        int span = hi - lo;
        if (span < 1) return;

        for (int j = 0; j < count; j++)
        {
            float u = count == 1 ? 0.5f : (j + 0.5f) / count;
            int anchor = lo + Mathf.RoundToInt(u * span);
            anchor += rng.Next(-1, 2); // -1, 0, or +1 knot of jitter
            anchor = Mathf.Clamp(anchor, lo, hi);
            apply(knots, anchor);
        }
    }

    private static void AddY(List<Vector3> knots, int index, float dy)
    {
        if (index < 0 || index >= knots.Count) return;
        Vector3 k = knots[index];
        k.y += dy;
        knots[index] = k;
    }

    // Sharp up/down/up reversal -> a hard, brief change of heading. High yaw
    // rate while grounded is exactly what StuntDetector reads as a Tight Turn.
    private static void Hairpin(List<Vector3> knots, int anchor, float sharpness, float spacing)
    {
        float h = Mathf.Lerp(spacing * 0.8f, spacing * 1.9f, Mathf.Clamp01(sharpness));
        AddY(knots, anchor - 1, -0.3f * h);
        AddY(knots, anchor, h);
        AddY(knots, anchor + 1, -h);
        AddY(knots, anchor + 2, 0.4f * h);
    }

    // Ramp up into a pinched convex crest, then a drop on the far side. The
    // crest curvature flings a car with any speed off the rail -> Airtime for a
    // long hang, Near Miss for a short skim.
    private static void Launch(List<Vector3> knots, int anchor, float strength, float spacing)
    {
        float h = Mathf.Lerp(spacing * 0.9f, spacing * 2.1f, Mathf.Clamp01(strength));
        AddY(knots, anchor - 2, 0.15f * h);
        AddY(knots, anchor - 1, 0.55f * h);
        AddY(knots, anchor, h);
        AddY(knots, anchor + 1, -0.2f * h);
        AddY(knots, anchor + 2, -0.05f * h);
    }

    // A deep smooth bowl: steep in, steep out. The descent side builds real
    // speed -> Speed Burst at the bottom.
    private static void Plunge(List<Vector3> knots, int anchor, float steepness, float spacing)
    {
        int half = Mathf.RoundToInt(Mathf.Lerp(2f, 3f, Mathf.Clamp01(steepness)));
        float depth = Mathf.Lerp(spacing * 1.0f, spacing * 1.8f, Mathf.Clamp01(steepness));
        for (int k = -half; k <= half; k++)
        {
            float t = (float)(k + half) / (2 * half); // 0..1
            AddY(knots, anchor + k, -depth * Mathf.Sin(t * Mathf.PI));
        }
    }

    // A run of quick alternating bumps -- short enough that a car only ever
    // skips off for a fraction of a second -> a string of Near Misses.
    private static void Washboard(List<Vector3> knots, int anchor, float strength, float spacing)
    {
        int span = Mathf.RoundToInt(Mathf.Lerp(5f, 9f, Mathf.Clamp01(strength)));
        float amp = Mathf.Lerp(spacing * 0.16f, spacing * 0.42f, Mathf.Clamp01(strength));
        int startK = -span / 2;
        for (int k = 0; k <= span; k++)
        {
            float fade = Mathf.Sin((float)k / span * Mathf.PI); // taper both ends
            float sign = (k % 2 == 0) ? 1f : -1f;
            AddY(knots, anchor + startK + k, amp * sign * fade);
        }
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
