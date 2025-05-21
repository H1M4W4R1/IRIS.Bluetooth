using IRIS.Operations.Abstract;
using IRIS.Operations.Generic;

namespace IRIS.Bluetooth.Operations
{
    /// <summary>
    ///     Represents a result when device is missing required characteristic.
    /// </summary>
    public readonly struct MissingRequiredCharacteristicResult : IDeviceOperationResult
    {
        /// <summary>
        ///     Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess => false;

        /// <summary>
        ///     Implicitly converts the result to a boolean indicating success.
        /// </summary>
        /// <param name="result">The result to convert.</param>
        public static implicit operator bool(MissingRequiredCharacteristicResult result) => result.IsSuccess;
    }
}
