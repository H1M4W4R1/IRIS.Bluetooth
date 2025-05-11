namespace IRIS.Bluetooth.Exceptions
{
    public sealed class MissingRequiredCharacteristicException()
        : Exception("Device is missing required characteristic");
}