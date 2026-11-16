using System;

namespace HandSeals.Core
{
    /// <summary>Axis-aligned box in image pixels, origin in the top-left corner.</summary>
    public readonly struct BoundingBox
    {
        public BoundingBox(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        public float XMin { get; }
        public float YMin { get; }
        public float XMax { get; }
        public float YMax { get; }

        public float Width => XMax - XMin;
        public float Height => YMax - YMin;
        public float CenterX => (XMin + XMax) * 0.5f;
        public float CenterY => (YMin + YMax) * 0.5f;

        public BoundingBox ClampTo(float width, float height)
        {
            return new BoundingBox(
                Math.Max(0f, XMin),
                Math.Max(0f, YMin),
                Math.Min(XMax, width),
                Math.Min(YMax, height));
        }

        public override string ToString()
        {
            return $"[{XMin:0.#}, {YMin:0.#}, {XMax:0.#}, {YMax:0.#}]";
        }
    }
}
