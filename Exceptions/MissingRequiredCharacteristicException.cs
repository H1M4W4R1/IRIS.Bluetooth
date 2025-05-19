namespace IRIS.Bluetooth.Exceptions
{
    /// <summary>
    /// Exception thrown when a required Bluetooth Low Energy characteristic is not found on a device.
    /// This exception is typically thrown by the **Require** methods
    /// when attempting to access a characteristic that is essential for device operation.
    /// </summary>
    public sealed class MissingRequiredCharacteristicException()
        : Exception("Device is missing required characteristic");
}