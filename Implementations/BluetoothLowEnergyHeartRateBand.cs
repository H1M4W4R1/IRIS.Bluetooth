using IRIS.Bluetooth.Common.Abstract;
using IRIS.Bluetooth.Devices;
using IRIS.Bluetooth.Implementations.Data;

namespace IRIS.Bluetooth.Implementations
{
    /// <summary>
    ///     Represents a Bluetooth Low Energy heart rate monitoring device that implements the Heart Rate GATT service.
    ///     This class handles the connection, configuration, and data processing for heart rate monitoring devices
    ///     that comply with the Bluetooth Heart Rate Profile specification.
    /// </summary>
    public sealed class BluetoothLowEnergyHeartRateBand() : BluetoothLowEnergyDeviceBase(HEART_RATE_SERVICE_UUID)
    {
        /// <summary>
        ///     The standard UUID for the Heart Rate Service as defined by the Bluetooth SIG.
        /// </summary>
        private const string HEART_RATE_SERVICE_UUID = "0000180D-0000-1000-8000-00805F9B34FB";

        /// <summary>
        ///     The standard UUID for the Heart Rate Measurement Characteristic as defined by the Bluetooth SIG.
        /// </summary>
        private const string HEART_RATE_CHARACTERISTIC_UUID = "00002A37-0000-1000-8000-00805F9B34FB";

        /// <summary>
        ///     The characteristic endpoint used for receiving heart rate measurements from the device.
        ///     This endpoint is configured during device initialization and is used to process incoming heart rate data.
        /// </summary>
        private IBluetoothLECharacteristic? HeartRateEndpoint { get; set; }

        /// <summary>
        ///     Configures the heart rate monitoring device by establishing the required characteristic endpoint
        ///     and setting up notification handling for heart rate measurements.
        /// </summary>
        public override async ValueTask Configure()
        {
            HeartRateEndpoint = await Require(HEART_RATE_SERVICE_UUID, HEART_RATE_CHARACTERISTIC_UUID,
                HandleHeartRateNotification);
        }

        /// <summary>
        ///     Delegate type for handling heart rate measurement events.
        /// </summary>
        /// <param name="heartRate">
        ///     The heart rate measurement data containing the current heart rate and energy expenditure
        ///     information.
        /// </param>
        public delegate void HeartRateReceivedHandler(HeartRateReadout heartRate);

        /// <summary>
        ///     Event that is raised when a new heart rate measurement is received from the device.
        ///     Subscribers will receive the processed heart rate data including the current heart rate
        ///     and any available energy expenditure information.
        /// </summary>
        public event HeartRateReceivedHandler OnHeartRateReceived = delegate { };

        /// <summary>
        ///     Processes incoming heart rate measurement notifications from the device.
        ///     Validates the data and triggers the OnHeartRateReceived event with the processed measurement.
        /// </summary>
        /// <param name="characteristic">The characteristic that generated the notification.</param>
        /// <param name="data">The raw heart rate measurement data received from the device.</param>
        private void HandleHeartRateNotification(IBluetoothLECharacteristic characteristic, byte[] data)
        {
            // If endpoint failed, return
            if (HeartRateEndpoint == null) return;

            // Skip if data is null or empty
            if (data.Length == 0) return;

            // Process the data
            HeartRateReadout heartRate = ProcessData(data);

            // Notify listeners
            OnHeartRateReceived(heartRate);
        }

        /// <summary>
        ///     Processes raw heart rate measurement data according to the Bluetooth Heart Rate Profile specification.
        ///     Interprets the data format flags and extracts the heart rate value and energy expenditure information.
        /// </summary>
        /// <param name="data">The raw byte array containing the heart rate measurement data.</param>
        /// <returns>A HeartRateReadout struct containing the processed heart rate and energy expenditure data.</returns>
        private HeartRateReadout ProcessData(byte[] data)
        {
            // Heart Rate profile defined flag values
            const byte HEART_RATE_VALUE_FORMAT = 0x01;
            const byte ENERGY_EXPANDED_STATUS = 0x08;

            byte currentOffset = 0;
            byte flags = data[currentOffset];
            bool isHeartRateValueSizeLong = ((flags & HEART_RATE_VALUE_FORMAT) != 0);
            bool hasEnergyExpended = ((flags & ENERGY_EXPANDED_STATUS) != 0);

            currentOffset++;

            ushort heartRateMeasurementValue;

            if (isHeartRateValueSizeLong)
            {
                heartRateMeasurementValue = (ushort) ((data[currentOffset + 1] << 8) + data[currentOffset]);
                currentOffset += 2;
            }
            else
            {
                heartRateMeasurementValue = data[currentOffset];
                currentOffset++;
            }

            ushort expendedEnergyValue = 0;

            if (hasEnergyExpended)
            {
                expendedEnergyValue = (ushort) ((data[currentOffset + 1] << 8) + data[currentOffset]);
                // currentOffset += 2;
            }

            // The Heart Rate Bluetooth profile can also contain sensor contact status information,
            // and R-Wave interval measurements, which can also be processed here. 
            // For the purpose of this sample, we don't need to interpret that data.
            return new HeartRateReadout
            {
                HeartRate = heartRateMeasurementValue,
                HasExpendedEnergy = hasEnergyExpended,
                ExpendedEnergy = expendedEnergyValue
            };
        }
    }
}