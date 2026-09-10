using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(SplineContainer))]
[ExecuteAlways] // Allows the line to update inside the Unity Editor without playing
public class SplineLineSampler : MonoBehaviour
{
    [SerializeField, Tooltip("Number of line segments. Higher numbers mean smoother lines.")]
    private int resolution = 30;

    private LineRenderer lineRenderer;
    private SplineContainer splineContainer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        splineContainer = GetComponent<SplineContainer>();
    }

    void Update()
    {
        if (splineContainer == null || lineRenderer == null || resolution < 2) return;

        // Sync the Line Renderer position count to our resolution
        lineRenderer.positionCount = resolution;

        for (int i = 0; i < resolution; i++)
        {
            // Calculate a normalized value from 0.0 (start) to 1.0 (end) of the spline
            float t = (float)i / (resolution - 1);

            // Evaluate the local position along the spline
            Vector3 localPos = splineContainer.EvaluatePosition(t);

            // If Line Renderer is using World Space, convert the local point to World Space
            if (lineRenderer.useWorldSpace)
            {
                Vector3 worldPos = transform.TransformPoint(localPos);
                lineRenderer.SetPosition(i, worldPos);
            }
            else
            {
                lineRenderer.SetPosition(i, localPos);
            }
        }
    }
}
