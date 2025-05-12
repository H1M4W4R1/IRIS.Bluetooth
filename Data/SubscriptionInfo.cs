using IRIS.Bluetooth.Common;
using IRIS.Bluetooth.Common.Abstract;

namespace IRIS.Bluetooth.Data
{
    /// <summary>
    ///     Contains info about subscription of characteristic value changed event to make it easily detachable
    /// </summary>
    public readonly struct SubscriptionInfo(IBluetoothLECharacteristic characteristic, CharacteristicValueChangedHandler callback)
    {
        public IBluetoothLECharacteristic Characteristic { get; } = characteristic;

        public CharacteristicValueChangedHandler Callback { get; } = callback;
    }
}