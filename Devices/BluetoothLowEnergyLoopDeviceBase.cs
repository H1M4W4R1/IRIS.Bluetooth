using System.Diagnostics;
using IRIS.Bluetooth.Common.Data;

namespace IRIS.Bluetooth.Devices
{
    /// <summary>
    ///     Base class for Bluetooth Low Energy devices that require continuous monitoring or periodic operations.
    ///     This class implements a background loop mechanism that executes device-specific actions at regular intervals
    ///     while the device is connected.
    /// </summary>
    public abstract class BluetoothLowEnergyLoopDeviceBase : BluetoothLowEnergyDeviceBase
    {
        /// <summary>
        ///     Cancellation token source used to gracefully terminate the device loop when the device is disposed
        ///     or disconnected. This ensures proper cleanup of background operations.
        /// </summary>
        protected readonly CancellationTokenSource deviceLoopCancellationTokenSource = new();

        /// <summary>
        ///     Controls the exception handling behavior of the device loop.
        ///     When set to true, exceptions in the loop will be propagated to the caller.
        ///     When false (default), exceptions are logged but not propagated.
        /// </summary>
        protected virtual bool ThrowLoopExceptions => false;

        /// <summary>
        ///     Abstract method that defines the core operations to be performed in each iteration of the device loop.
        ///     Implementations should include device-specific monitoring, data collection, or control operations.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        protected abstract ValueTask OnDeviceLoop(CancellationToken cancellationToken = default);

        /// <summary>
        ///     Initializes and manages the background loop for device operations.
        ///     The loop continues until cancellation is requested, checking device connection status
        ///     and executing device-specific operations at each iteration.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the loop operation.</param>
        private async void StartDeviceLoop(CancellationToken cancellationToken = default)
        {
            // Perform loop actions here
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check if the device is connected
                    if (!IsConnected)
                    {
                        // Wait and ignore everything else
                        await Task.Delay(25, cancellationToken);
                        continue;
                    }

                    // Call the loop method
                    await OnDeviceLoop(cancellationToken);
                }
                catch (Exception anyException)
                {
                    // Check if we should throw exceptions
                    if (ThrowLoopExceptions) throw;

                    // Log the exception if we are not throwing it
                    Debug.WriteLine(anyException, "BluetoothLowEnergyLoopDeviceBase: Exception in device loop");
                }
            }
        }

        /// <summary>
        ///     Initializes a new instance of the BluetoothLowEnergyLoopDeviceBase class using a regex pattern for device
        ///     identification.
        /// </summary>
        /// <param name="regexPattern">The regex pattern to match the device name or identifier.</param>
        /// <param name="regexType">The type of regex matching to perform (defaults to Name).</param>
        public BluetoothLowEnergyLoopDeviceBase(string regexPattern, RegexType regexType = RegexType.Name) : base(
            regexPattern, regexType)
        {
            StartDeviceLoop(deviceLoopCancellationTokenSource.Token);
        }

        /// <summary>
        ///     Initializes a new instance of the BluetoothLowEnergyLoopDeviceBase class using a specific service UUID.
        /// </summary>
        /// <param name="serviceUUID">The UUID of the service to connect to.</param>
        public BluetoothLowEnergyLoopDeviceBase(Guid serviceUUID) : base(serviceUUID)
        {
            StartDeviceLoop(deviceLoopCancellationTokenSource.Token);
        }

        /// <summary>
        ///     Initializes a new instance of the BluetoothLowEnergyLoopDeviceBase class using a Bluetooth LE address.
        /// </summary>
        /// <param name="bleAddress">The Bluetooth LE address of the device to connect to.</param>
        public BluetoothLowEnergyLoopDeviceBase(ulong bleAddress) : base(bleAddress)
        {
            StartDeviceLoop(deviceLoopCancellationTokenSource.Token);
        }
    }
}