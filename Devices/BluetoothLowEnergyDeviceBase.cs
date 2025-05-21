using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using IRIS.Bluetooth.Common;
using IRIS.Bluetooth.Common.Abstract;
using IRIS.Bluetooth.Common.Addressing;
using IRIS.Bluetooth.Common.Data;
using IRIS.Bluetooth.Data;
using IRIS.Bluetooth.Exceptions;
using IRIS.Devices;
using IRIS.Operations;
using IRIS.Operations.Abstract;
using IRIS.Operations.Connection;
#if OS_WINDOWS
using IRIS.Bluetooth.Windows.Communication;

#elif OS_LINUX
    using IRIS.Bluetooth.Linux.Communication;
#endif

namespace IRIS.Bluetooth.Devices
{
    /// <summary>
    ///     Base class for Bluetooth Low Energy devices that provides common functionality for BLE device management,
    ///     characteristic handling, and platform-specific implementations.
    /// </summary>
    public abstract class BluetoothLowEnergyDeviceBase : DeviceBase<IBluetoothLEInterface>
    {
        /// <summary>
        ///     Maintains a list of all active event subscriptions for this device.
        ///     Used to properly clean up event handlers when the device is disconnected.
        /// </summary>
        private readonly List<SubscriptionInfo> _eventSubscriptions = new();

        /// <summary>
        ///     Indicates whether the device is currently connected to the hardware layer.
        ///     Returns true if the Device property is not null.
        /// </summary>
        public bool IsConnected => Device != null;

        /// <summary>
        ///     Indicates whether the device is fully initialized and ready for use.
        ///     This flag accounts for platform-specific initialization delays, particularly on Linux
        ///     where the BLE stack may require additional time to stabilize.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        ///     Reference to the current hardware layer device implementation.
        ///     Null when the device is not connected.
        /// </summary>
        public IBluetoothLEDevice? Device { get; private set; }

        /// <summary>
        ///     Initializes a new BLE device using a regex pattern to match device names or service UUIDs.
        /// </summary>
        /// <param name="regexPattern">The regex pattern to match against device names or service UUIDs</param>
        /// <param name="regexType">Specifies whether the pattern matches against device names or service UUIDs</param>
        protected BluetoothLowEnergyDeviceBase(string regexPattern, RegexType regexType = RegexType.Name) : this(
            regexType == RegexType.Name
                ? new BluetoothLENameAddress(regexPattern)
                : new BluetoothLEServiceRegexAddress(regexPattern))
        {
        }

        /// <summary>
        ///     Initializes a new BLE device using a specific service UUID.
        /// </summary>
        /// <param name="serviceUUID">The UUID of the service to connect to</param>
        protected BluetoothLowEnergyDeviceBase(Guid serviceUUID) : this(
            new BluetoothLEServiceAddress(serviceUUID.ToString()))
        {
        }

        /// <summary>
        ///     Initializes a new BLE device using a specific BLE address.
        /// </summary>
        /// <param name="bleAddress">The 64-bit BLE address of the device</param>
        protected BluetoothLowEnergyDeviceBase(ulong bleAddress) : this(
            new BluetoothLEDeviceIdentifierAddress(bleAddress))
        {
        }

        /// <summary>
        ///     Initializes a new BLE device using a custom address implementation.
        ///     Sets up platform-specific hardware access and event handlers.
        /// </summary>
        /// <param name="address">The BLE address implementation to use for device discovery</param>
        /// <exception cref="NotSupportedException">Thrown when the current platform is not supported</exception>
        protected BluetoothLowEnergyDeviceBase(IBluetoothLEAddress address)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
#if OS_LINUX
                HardwareAccess =
 new LinuxBluetoothLEInterface(address);
#endif
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // HardwareAccess = new MacOSBluetoothLEInterface(address); // TODO: Uncomment this line when MacOS implementation is available
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                throw new NotSupportedException();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#if OS_WINDOWS
                HardwareAccess = new WindowsBluetoothLEInterface(address);
#else
                throw new NotSupportedException("To make it work on Windows you need to explicitly use Windows .NET Runtime API.");
#endif
            }

            HardwareAccess.OnBluetoothDeviceConnected += OnDeviceConnected;
            HardwareAccess.OnBluetoothDeviceDisconnected += OnDeviceDisconnected;
        }

        /// <summary>
        ///     Finalizer to ensure proper cleanup of event handlers.
        /// </summary>
        ~BluetoothLowEnergyDeviceBase()
        {
            HardwareAccess.OnBluetoothDeviceConnected -= OnDeviceConnected;
            HardwareAccess.OnBluetoothDeviceDisconnected -= OnDeviceDisconnected;
        }

        /// <summary>
        ///     Abstract method that must be implemented by derived classes to handle device-specific configuration.
        ///     This is called after the device is connected and before it is marked as ready.
        /// </summary>
        public abstract ValueTask Configure();

        /// <summary>
        ///     Detaches all event handlers from the device's characteristics and clears the subscription list.
        ///     Called during device cleanup to prevent memory leaks.
        /// </summary>
        protected void _DetachEvents()
        {
            lock (_eventSubscriptions)
            {
                for (int n = _eventSubscriptions.Count - 1; n >= 0; n--)
                {
                    _eventSubscriptions[n].Characteristic.ValueChanged -= _eventSubscriptions[n].Callback;
                    _eventSubscriptions.RemoveAt(n);
                }
            }
        }

        /// <summary>
        ///     Internal method that handles device configuration and sets the ready flag.
        ///     Called after device connection is established.
        /// </summary>
        private async void _ConfigureDevice()
        {
            try
            {
                await Configure();
                IsReady = true;
            }
            catch (MissingRequiredCharacteristicException)
            {
                ReleaseDevice();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error configuring device: {e}");
                ReleaseDevice();
            }
        }

        /// <summary>
        ///     Requires a characteristic with the specified UUID pattern and callback.
        ///     Throws MissingRequiredCharacteristicException if the characteristic is not found.
        /// </summary>
        public ValueTask<IBluetoothLECharacteristic> Require(
            string characteristicUUIDRegex,
            CharacteristicValueChangedHandler callback,
            CharacteristicFlags flags = CharacteristicFlags.None) =>
            Require(null, characteristicUUIDRegex, callback, flags);

        /// <summary>
        ///     Requires a characteristic with the specified service UUID pattern and flags.
        ///     Throws MissingRequiredCharacteristicException if the characteristic is not found.
        /// </summary>
        public async ValueTask<IBluetoothLECharacteristic> Require(
            string? serviceUUIDRegex,
            CharacteristicFlags flags,
            CharacteristicValueChangedHandler callback)
        {
            IBluetoothLECharacteristic? characteristic = await Use(serviceUUIDRegex, flags, callback);
            if (characteristic == null) throw new MissingRequiredCharacteristicException();
            return characteristic;
        }

        /// <summary>
        ///     Requires a characteristic with the specified service and characteristic UUID patterns.
        ///     Throws MissingRequiredCharacteristicException if the characteristic is not found.
        /// </summary>
        public async ValueTask<IBluetoothLECharacteristic> Require(
            string? serviceUUIDRegex,
            string characteristicUUIDRegex,
            CharacteristicValueChangedHandler callback,
            CharacteristicFlags flags = CharacteristicFlags.None)
        {
            IBluetoothLECharacteristic? characteristic =
                await Use(serviceUUIDRegex, characteristicUUIDRegex, callback, flags);
            if (characteristic == null) throw new MissingRequiredCharacteristicException();
            return characteristic;
        }

        /// <summary>
        ///     Requires a characteristic with the specified service UUID pattern and flags.
        ///     Throws MissingRequiredCharacteristicException if the characteristic is not found.
        /// </summary>
        public async ValueTask<IBluetoothLECharacteristic> Require(
            string? serviceUUIDRegex,
            CharacteristicFlags flags)
        {
            IBluetoothLECharacteristic? characteristic = await Use(serviceUUIDRegex, flags);
            if (characteristic == null) throw new MissingRequiredCharacteristicException();
            return characteristic;
        }

        /// <summary>
        ///     Requires a characteristic with the specified service and characteristic UUID patterns.
        ///     Throws MissingRequiredCharacteristicException if the characteristic is not found.
        /// </summary>
        public async ValueTask<IBluetoothLECharacteristic> Require(
            string? serviceUUIDRegex,
            string characteristicUUIDRegex,
            CharacteristicFlags flags = CharacteristicFlags.None)
        {
            IBluetoothLECharacteristic? characteristic =
                await Use(serviceUUIDRegex, characteristicUUIDRegex, flags);
            if (characteristic == null) throw new MissingRequiredCharacteristicException();
            return characteristic;
        }

        /// <summary>
        ///     Attempts to use a characteristic with the specified UUID pattern and callback.
        ///     Returns null if the characteristic is not found.
        /// </summary>
        /// <param name="characteristicUUIDRegex">The regex pattern to match the characteristic UUID.</param>
        /// <param name="callback">The handler to be called when the characteristic's value changes.</param>
        /// <param name="flags">Optional flags to filter characteristics.</param>
        public ValueTask<IBluetoothLECharacteristic?> Use(
            string characteristicUUIDRegex,
            CharacteristicValueChangedHandler callback,
            CharacteristicFlags flags = CharacteristicFlags.None) =>
            Use(null, characteristicUUIDRegex, callback, flags);

        /// <summary>
        ///     Attempts to use a characteristic with the specified service UUID pattern, flags, and callback.
        ///     Returns null if the characteristic is not found.
        /// </summary>
        /// <param name="serviceUUIDRegex">The regex pattern to match the service UUID, or null to search all services.</param>
        /// <param name="flags">Flags to filter characteristics.</param>
        /// <param name="callback">The handler to be called when the characteristic's value changes.</param>
        public async ValueTask<IBluetoothLECharacteristic?> Use(
            string? serviceUUIDRegex,
            CharacteristicFlags flags,
            CharacteristicValueChangedHandler callback)
        {
            // Get characteristic
            IBluetoothLECharacteristic? characteristic = await Use(serviceUUIDRegex, flags);

            // Check if characteristic is not null
            if (characteristic == null) return null;

            await SubscribeCharacteristic(characteristic, callback);

            // Return characteristic
            return characteristic;
        }

        /// <summary>
        ///     Attempts to use a characteristic with the specified service and characteristic UUID patterns and callback.
        ///     Returns null if the characteristic is not found.
        /// </summary>
        /// <param name="serviceUUIDRegex">The regex pattern to match the service UUID, or null to search all services.</param>
        /// <param name="characteristicUUIDRegex">The regex pattern to match the characteristic UUID.</param>
        /// <param name="callback">The handler to be called when the characteristic's value changes.</param>
        /// <param name="flags">Optional flags to filter characteristics.</param>
        public async ValueTask<IBluetoothLECharacteristic?> Use(
            string? serviceUUIDRegex,
            string characteristicUUIDRegex,
            CharacteristicValueChangedHandler callback,
            CharacteristicFlags flags
                = CharacteristicFlags.None)
        {
            // Get characteristic
            IBluetoothLECharacteristic? characteristic =
                await Use(serviceUUIDRegex, characteristicUUIDRegex, flags);

            // Check if characteristic is not null
            if (characteristic == null) return null;

            await SubscribeCharacteristic(characteristic, callback);

            // Return characteristic
            return characteristic;
        }

        /// <summary>
        ///     Subscribes to a characteristic's value changes and registers the subscription for cleanup.
        /// </summary>
        /// <param name="characteristic">The characteristic to subscribe to.</param>
        /// <param name="callback">The handler to be called when the characteristic's value changes.</param>
        private async ValueTask SubscribeCharacteristic(
            IBluetoothLECharacteristic characteristic,
            CharacteristicValueChangedHandler callback)
        {
            // Attach handler
            characteristic.ValueChanged += callback;

            // Register subscription
            lock (_eventSubscriptions) _eventSubscriptions.Add(new SubscriptionInfo(characteristic, callback));

            // Subscribe to notifications
            await characteristic.SubscribeAsync();
        }

        /// <summary>
        ///     Attempts to find a characteristic matching the specified service UUID pattern and flags.
        ///     Returns null if no matching characteristic is found.
        /// </summary>
        /// <param name="serviceUUIDRegex">The regex pattern to match the service UUID, or null to search all services.</param>
        /// <param name="flags">Flags to filter characteristics.</param>
        public ValueTask<IBluetoothLECharacteristic?> Use(string? serviceUUIDRegex, CharacteristicFlags flags)
        {
            // Check if device is not null
            if (Device == null) return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);

            // Check if service is null and acquire characteristics based on that
            IDeviceOperationResult opResult = string.IsNullOrEmpty(serviceUUIDRegex)
                ? Device.GetAllCharacteristics()
                : Device.GetAllCharacteristicsForServices(serviceUUIDRegex);

            // Check if operation failed
            // TODO: Handle custom failure scenarios?
            if (DeviceOperation.IsFailure(opResult)) 
                return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);
            
            // Acquire data from operation result
            if(opResult is not IDeviceOperationResult<IReadOnlyList<IBluetoothLECharacteristic>> dataResult)
                return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);
            
            // Check if any characteristics were found
            IBluetoothLECharacteristic? foundCharacteristic =
                dataResult.Data.FirstOrDefault(c => c.IsValidForFlags(flags));

            return ValueTask.FromResult(foundCharacteristic);
        }

        /// <summary>
        ///     Attempts to find a characteristic matching the specified service and characteristic UUID patterns.
        ///     Returns null if no matching characteristic is found.
        /// </summary>
        /// <param name="serviceUUIDRegex">The regex pattern to match the service UUID, or null to search all services.</param>
        /// <param name="characteristicUUIDRegex">The regex pattern to match the characteristic UUID.</param>
        /// <param name="flags">Optional flags to filter characteristics.</param>
        public ValueTask<IBluetoothLECharacteristic?> Use(
            string? serviceUUIDRegex,
            string characteristicUUIDRegex,
            CharacteristicFlags flags = CharacteristicFlags.None)
        {
            // Check if device is not null
            if (Device == null) return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);

            // Check if service is null and acquire characteristics based on that
            IDeviceOperationResult opResult = string.IsNullOrEmpty(serviceUUIDRegex)
                ? Device.GetAllCharacteristics()
                : Device.GetAllCharacteristicsForServices(serviceUUIDRegex);

            // Check if operation failed
            // TODO: Handle custom failure scenarios?
            if (DeviceOperation.IsFailure(opResult)) 
                return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);
            
            // Acquire data from operation result
            if(opResult is not IDeviceOperationResult<IReadOnlyList<IBluetoothLECharacteristic>> dataResult)
                return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);
            
            // Check if any characteristics were found
            if (dataResult.Data.Count < 1) return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);

            // Check if the characteristic is not null
            IBluetoothLECharacteristic? characteristic = dataResult
                .Data.FirstOrDefault(c => Regex.IsMatch(c.UUID, characteristicUUIDRegex));

            return ValueTask.FromResult(characteristic);
        }

        /// <summary>
        ///     Establishes a connection to the Bluetooth LE device.
        ///     This method handles the connection process, including hardware access initialization,
        ///     device claiming, and initial configuration.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public override async ValueTask<IDeviceOperationResult> Connect(
            CancellationToken cancellationToken = default)
        {
            // Connect to hardware access (ensure discovery is started)
            if (DeviceOperation.IsFailure(await HardwareAccess.Connect(cancellationToken)))
                return DeviceOperation.Result<DeviceConnectionFailedResult>();

            // Wait for free device to appear
            Device = await HardwareAccess.ClaimDevice(cancellationToken);

            // Check if device was acquired correctly
            if (Device == null) DeviceOperation.Result<DeviceNotFoundResult>();

            // Wait a while for device to be connected properly
            // as BLE seems to have small issues when this is not provided
            await Task.Delay(25, cancellationToken);

            // Configure device as OnDeviceConnected won't be called
            _ConfigureDevice();

            return DeviceOperation.Result<DeviceConnectedSuccessfullyResult>();
        }

        /// <summary>
        ///     Disconnects from the Bluetooth LE device and releases associated resources.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public override ValueTask<IDeviceOperationResult> Disconnect(CancellationToken cancellationToken = default)
        {
            // Prevent doing any operations on this device
            IsReady = false;

            // Check if device is null, we assume null means 
            // that device was already disconnected
            if (Device == null) return DeviceOperation.VResult<DeviceAlreadyDisconnectedResult>();

            // Release device
            ReleaseDevice();
            return DeviceOperation.VResult<DeviceDisconnectedSuccessfullyResult>();
        }

        /// <summary>
        ///     Releases the device and cleans up associated resources.
        ///     This method handles event detachment and device release through the hardware access layer.
        /// </summary>
        private async void ReleaseDevice()
        {
            // Check if device is null
            if (Device == null) return;

            // Device is not ready anymore (ensure proper value)
            IsReady = false;

            // Detach events and release device
            _DetachEvents();
            await HardwareAccess.ReleaseDevice(Device);
            Device = null;
        }

        /// <summary>
        ///     Handles the device connected event.
        ///     Configures the device when it is successfully connected.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="device">The connected device.</param>
        private void OnDeviceConnected(IBluetoothLEInterface sender, IBluetoothLEDevice device)
        {
            if (device != Device) return;

            // Device is connected, configure this device
            _ConfigureDevice();
        }

        /// <summary>
        ///     Handles the device disconnected event.
        ///     Ensures proper cleanup of device resources and state when the device is disconnected.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="device">The disconnected device.</param>
        private async void OnDeviceDisconnected(IBluetoothLEInterface sender, IBluetoothLEDevice device)
        {
            if (device != Device) return;

            // Disconnect when device is disconnected
            await Disconnect();
            IsReady = false;
        }
    }
}