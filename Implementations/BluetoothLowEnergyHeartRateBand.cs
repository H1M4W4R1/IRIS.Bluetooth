using IRIS.Bluetooth.Common.Abstract;
using IRIS.Bluetooth.Devices;
using IRIS.Bluetooth.Implementations.Data;

namespace IRIS.Bluetooth.Implementations
{
    /// <summary>
    /// Heart Rate Band Bluetooth Low Energy device.
    /// Uses the HeartRate GATT service.
    /// </summary>
    public sealed class BluetoothLowEnergyHeartRateBand() : BluetoothLowEnergyDeviceBase(HEART_RATE_SERVICE_UUID)
    {
        private const string HEART_RATE_SERVICE_UUID = "0000180D-0000-1000-8000-00805F9B34FB";
        private const string HEART_RATE_CHARACTERISTIC_UUID = "00002A37-0000-1000-8000-00805F9B34FB";

        /// <summary>
        ///     Endpoint ID for the heart rate characteristic.
        /// </summary>
        private IBluetoothLECharacteristic? HeartRateEndpoint { get; set; }

        public override async ValueTask Configure()
        {
            HeartRateEndpoint = await Require(HEART_RATE_SERVICE_UUID, HEART_RATE_CHARACTERISTIC_UUID,
                HandleHeartRateNotification);
        }

        /// <summary>
        /// Handler for when a heart rate is received
        /// </summary>
        public delegate void HeartRateReceivedHandler(HeartRateReadout heartRate);

        /// <summary>
        /// Event for when a heart rate is received
        /// </summary>
        public event HeartRateReceivedHandler OnHeartRateReceived = delegate { };

        /// <summary>
        /// Process the raw data received from the device into application usable data,
        /// </summary>
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
        /// Process the raw data received from the device into application usable data, 
        /// according the the Bluetooth Heart Rate Profile.
        /// </summary>
        /// <param name="data">Raw data received from the heart rate monitor.</param>
        /// <returns>The heart rate measurement value.</returns>
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