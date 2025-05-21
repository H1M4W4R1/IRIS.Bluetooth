using IRIS.Bluetooth.Common;
using IRIS.Bluetooth.Common.Abstract;

namespace IRIS.Bluetooth.Data
{
    /// <summary>
    ///     Represents information about a subscription to a Bluetooth LE characteristic's value changed event.
    ///     This struct encapsulates the characteristic and its associated callback handler to facilitate
    ///     easy subscription management and detachment.
    /// </summary>
    /// <param name="characteristic">The Bluetooth LE characteristic being monitored.</param>
    /// <param name="callback">The handler that will be invoked when the characteristic's value changes.</param>
    public readonly struct SubscriptionInfo(IBluetoothLECharacteristic characteristic, CharacteristicValueChangedHandler callback)
    {
        /// <summary>
        ///     Gets the Bluetooth LE characteristic associated with this subscription.
        /// </summary>
        public IBluetoothLECharacteristic Characteristic { get; } = characteristic;

        /// <summary>
        ///     Gets the callback handler that will be invoked when the characteristic's value changes.
        /// </summary>
        public CharacteristicValueChangedHandler Callback { get; } = callback;
    }
}