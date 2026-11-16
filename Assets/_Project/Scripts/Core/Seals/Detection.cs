namespace HandSeals.Core
{
    /// <summary>One seal found by the detector in a camera frame.</summary>
    public readonly struct Detection
    {
        public Detection(SealType seal, float score, BoundingBox box)
        {
            Seal = seal;
            Score = score;
            Box = box;
        }

        public SealType Seal { get; }

        /// <summary>Confidence in the range 0..1 (objectness multiplied by class probability).</summary>
        public float Score { get; }

        /// <summary>Box in pixels of the upright camera image.</summary>
        public BoundingBox Box { get; }

        public override string ToString()
        {
            return $"{Seal} {Score:0.00} {Box}";
        }
    }
}
