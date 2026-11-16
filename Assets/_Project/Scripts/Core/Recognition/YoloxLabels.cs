namespace HandSeals.Core
{
    /// <summary>
    /// Maps class ids of the YOLOX hand-sign model to <see cref="SealType"/>.
    /// The model's class 0 is Rat: its ids are shifted by one against labels.csv,
    /// whose first line is the "None" placeholder.
    /// </summary>
    public static class YoloxLabels
    {
        /// <summary>Number of class scores in each output row of the model (21 values = 4 box + 1 objectness + 16 classes).</summary>
        public const int ClassCount = 16;

        public static SealType ToSeal(int classId)
        {
            int value = classId + 1;
            if (classId < 0 || value > (int)SealType.Mizunoe)
            {
                return SealType.Unknown;
            }

            return (SealType)value;
        }
    }
}
