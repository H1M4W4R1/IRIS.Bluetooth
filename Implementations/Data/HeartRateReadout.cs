namespace IRIS.Bluetooth.Implementations.Data
{
    /// <summary>
    ///     Represents a heart rate measurement readout from a Bluetooth Low Energy heart rate monitor device.
    ///     This struct contains the current heart rate, energy expenditure data, and a timestamp of when the measurement was
    ///     taken.
    /// </summary>
    public struct HeartRateReadout()
    {
        /// <summary>
        ///     Gets or sets the current heart rate measurement in beats per minute (BPM).
        ///     A value of 0 indicates no valid measurement.
        /// </summary>
        public ushort HeartRate { get; set; } = 0;

        /// <summary>
        ///     Gets or sets a value indicating whether the device has calculated energy expenditure.
        ///     When true, the ExpendedEnergy property contains valid data.
        /// </summary>
        public bool HasExpendedEnergy { get; set; } = false;

        /// <summary>
        ///     Gets or sets the amount of energy expended in kilo-calories (kcal).
        ///     This value is only valid when HasExpendedEnergy is true.
        /// </summary>
        public ushort ExpendedEnergy { get; set; } = 0;

        /// <summary>
        ///     Gets or sets the UTC timestamp when this heart rate measurement was taken.
        ///     Defaults to the current UTC time when the struct is initialized.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        ///     Returns a string representation of the heart rate measurement.
        /// </summary>
        /// <returns>A string containing the heart rate value in BPM.</returns>
        public override string ToString() => HeartRate.ToString();
    }
}