using UnityEngine;

namespace Ferrous.SpriteStack
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FerrousSpriteStack : MonoBehaviour
    {
        [SerializeField] private Transform[] layers;
        [SerializeField] private int[] layerHeights;
        [SerializeField] private float pixelsPerUnit = 32f;
        [SerializeField, Range(0f, 360f)] private float heading;
        [SerializeField, Range(0.15f, 1f)] private float squash = 0.65f;
        [SerializeField, Min(0f)] private float sliceSpacingPixels = 1f;

        private float lastHeading = float.NaN;
        private float lastSquash, lastSpacing, lastPixelsPerUnit;

        public float Heading => heading;

        public void SetHeading(float degrees)
        {
            heading = Mathf.Repeat(degrees, 360f);
            ApplyIfChanged();
        }

        public void Configure(Transform[] pivots, int[] heights, float ppu,
            float startingHeading, float projectionSquash, float spacing)
        {
            layers = pivots;
            layerHeights = heights;
            pixelsPerUnit = ppu;
            heading = startingHeading;
            squash = projectionSquash;
            sliceSpacingPixels = spacing;
            lastHeading = float.NaN;
            ApplyIfChanged();
        }

        private void OnEnable()
        {
            lastHeading = float.NaN;
            ApplyIfChanged();
        }

        private void Update() => ApplyIfChanged();

        private void ApplyIfChanged()
        {
            if (layers == null || layerHeights == null || layers.Length != layerHeights.Length) return;
            if (heading == lastHeading && squash == lastSquash && sliceSpacingPixels == lastSpacing &&
                pixelsPerUnit == lastPixelsPerUnit) return;

            float yScale = Mathf.Clamp(squash, 0.15f, 1f);
            float spacing = Mathf.Max(0f, sliceSpacingPixels) / Mathf.Max(1f, pixelsPerUnit);
            Quaternion rotation = Quaternion.Euler(0f, 0f, heading);
            for (int i = 0; i < layers.Length; i++)
            {
                Transform pivot = layers[i];
                if (pivot == null || pivot.childCount == 0) continue;
                pivot.localPosition = Vector3.up * (layerHeights[i] * spacing);
                pivot.localScale = new Vector3(1f, yScale, 1f);
                pivot.GetChild(0).localRotation = rotation;
            }
            lastHeading = heading;
            lastSquash = squash;
            lastSpacing = sliceSpacingPixels;
            lastPixelsPerUnit = pixelsPerUnit;
        }
    }
}
