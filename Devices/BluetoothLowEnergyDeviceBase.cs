using System.Diagnostics;
using System.Text.RegularExpressions;
using IRIS.Bluetooth.Common;
using IRIS.Bluetooth.Common.Abstract;
using IRIS.Bluetooth.Common.Addressing;
using IRIS.Bluetooth.Common.Data;
using IRIS.Bluetooth.Data;
using IRIS.Bluetooth.Exceptions;
using IRIS.Devices;

#if OS_WINDOWS
using IRIS.Bluetooth.Windows.Communication;

#elif OS_LINUX
using IRIS.Bluetooth.Linux.Communication;

#endif

namespace IRIS.Bluetooth.Devices
{
    /// <summary>
    ///     Base class for Bluetooth Low Energy devices.
    /// </summary>
    public abstract class BluetoothLowEnergyDeviceBase : BluetoothLowEnergyDeviceBaseInternal
    {
        /// <summary>
        ///     List of all subscriptions on this device
        /// </summary>
        private List<SubscriptionInfo> _eventSubscriptions = new();
        
        /// <summary>
        ///     Defines if device is connected to the hardware layer.
        /// </summary>
        public bool IsConnected => Device != null;

        /// <summary>
        ///     Defines if device is ready to be used. Sometimes it might be necessary to delay
        ///     configuration of the device until the hardware layer is ready. Linux API is sometimes trashy.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        ///     Device reference for the device that is currently used as hardware layer.
        /// </summary>
        public IBluetoothLEDevice? Device { get; private set; }

        protected BluetoothLowEnergyDeviceBase(string regexPattern, RegexType regexType = RegexType.Name) : this(
            regexType == RegexType.Name
                ? new BluetoothLENameAddress(regexPattern)
                : new BluetoothLEServiceRegexAddress(regexPattern))
        {
        }

        protected BluetoothLowEnergyDeviceBase(Guid serviceUUID) : this(
            new BluetoothLEServiceAddress(serviceUUID.ToString()))
        {
        }

        protected BluetoothLowEnergyDeviceBase(ulong bleAddress) : this(
            new BluetoothLEDeviceIdentifierAddress(bleAddress))
        {
        }

        protected BluetoothLowEnergyDeviceBase(IBluetoothLEAddress address) : base(address)
        {
            HardwareAccess.OnBluetoothDeviceConnected += OnDeviceConnected;
            HardwareAccess.OnBluetoothDeviceDisconnected += OnDeviceDisconnected;
        }

        ~BluetoothLowEnergyDeviceBase()
        {
            HardwareAccess.OnBluetoothDeviceConnected -= OnDeviceConnected;
            HardwareAccess.OnBluetoothDeviceDisconnected -= OnDeviceDisconnected;
        }

        /// <summary>
        ///     Handles the configuration of the device.
        /// </summary>
        public abstract ValueTask Configure();

        protected void _DetachEvents()
        {
            for (int n = _eventSubscriptions.Count - 1; n >= 0; n--)
            {
                // Unsubscribe event
                _eventSubscriptions[n].Characteristic.ValueChanged -= _eventSubscriptions[n].Callback;
                _eventSubscriptions.RemoveAt(n);
            }
        }
        
        /// <summary>
        ///     Internal method to configure the device and set proper flags
        ///     also searches for valid endpoints
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

        public ValueTask<IBluetoothLECharacteristic?> Require(
            string characteristicUUIDRegex,
            CharacteristicValueChanged callback,
            CharacteristicFlags flags = CharacteristicFlags.None) =>
            Require(null, characteristicUUIDRegex, callback, flags);

        public async ValueTask<IBluetoothLECharacteristic?> Require(
            string? serviceUUIDRegex,
            CharacteristicFlags flags,
            CharacteristicValueChanged callback)
        {
            // Get the characteristic
            IBluetoothLECharacteristic?
                characteristic = await Use(serviceUUIDRegex, flags, callback);

            // Check if characteristic is null
            if (characteristic == null) throw new MissingRequiredCharacteristicException();

            // Return characteristic
            return characteristic;
        }

        public async ValueTask<IBluetoothLECharacteristic?> Require(
            string? serviceUUIDRegex,
            string characteristicUUIDRegex,
            CharacteristicValueChanged callback,
            CharacteristicFlags flags = CharacteristicFlags.None)
        {
            // Get the characteristic
            IBluetoothLECharacteristic? characteristic =
                await Use(serviceUUIDRegex, characteristicUUIDRegex, callback, flags);

            // Check if characteristic is null
            if (characteristic == null) throw new MissingRequiredCharacteristicException();

            // Return characteristic
            return characteristic;
        }

        public async ValueTask<IBluetoothLECharacteristic?> Require(
            string? serviceUUIDRegex,
            CharacteristicFlags flags)
        {
            // Get the characteristic
            IBluetoothLECharacteristic? characteristic = await Use(serviceUUIDRegex, flags);

            // Check if characteristic is null
            if (characteristic == null) throw new MissingRequiredCharacteristicException();

            // Return characteristic
            return characteristic;
        }

        public async ValueTask<IBluetoothLECharacteristic?> Require(
            string? serviceUUIDRegex,
            string characteristicUUIDRegex,
            CharacteristicFlags flags = CharacteristicFlags.None)
        {
            // Get the characteristic
            IBluetoothLECharacteristic? characteristic =
                await Use(serviceUUIDRegex, characteristicUUIDRegex, flags);

            // Check if characteristic is null
            if (characteristic == null) throw new MissingRequiredCharacteristicException();

            // Return characteristic
            return characteristic;
        }

        public ValueTask<IBluetoothLECharacteristic?> Use(
            string characteristicUUIDRegex,
            CharacteristicValueChanged callback,
            CharacteristicFlags flags = CharacteristicFlags.None) =>
            Use(null, characteristicUUIDRegex, callback, flags);

        public async ValueTask<IBluetoothLECharacteristic?> Use(
            string? serviceUUIDRegex,
            CharacteristicFlags flags,
            CharacteristicValueChanged callback)
        {
            // Get characteristic
            IBluetoothLECharacteristic? characteristic = await Use(serviceUUIDRegex, flags);

            // Check if characteristic is not null
            if (characteristic == null) return null;

            await SubscribeCharacteristic(characteristic, callback);

            // Return characteristic
            return characteristic;
        }

        public async ValueTask<IBluetoothLECharacteristic?> Use(
            string? serviceUUIDRegex,
            string characteristicUUIDRegex,
            CharacteristicValueChanged callback,
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

        private async ValueTask SubscribeCharacteristic(
            IBluetoothLECharacteristic characteristic,
            CharacteristicValueChanged callback)
        {
            // Attach handler
            characteristic.ValueChanged += callback;

            // Register subscription
            _eventSubscriptions.Add(new SubscriptionInfo(characteristic, callback));

            // Subscribe to notifications
            await characteristic.SubscribeAsync();
        }

        public ValueTask<IBluetoothLECharacteristic?> Use(string? serviceUUIDRegex, CharacteristicFlags flags)
        {
            // Check if device is not null
            if (Device == null) return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);

            // Check if service is null and acquire characteristics based on that
            IReadOnlyList<IBluetoothLECharacteristic> characteristics = string.IsNullOrEmpty(serviceUUIDRegex)
                ? Device.GetAllCharacteristics()
                : Device.GetAllCharacteristicsForServices(serviceUUIDRegex);

            // Check if any characteristics were found
            IBluetoothLECharacteristic? foundCharacteristic =
                characteristics.FirstOrDefault(c => c.IsValidForFlags(flags));

            return ValueTask.FromResult<IBluetoothLECharacteristic?>(foundCharacteristic);
        }

        public ValueTask<IBluetoothLECharacteristic?> Use(
            string? serviceUUIDRegex,
            string characteristicUUIDRegex,
            CharacteristicFlags flags = CharacteristicFlags.None)
        {
            // Check if device is not null
            if (Device == null) return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);

            // Check if service is null and acquire characteristics based on that
            IReadOnlyList<IBluetoothLECharacteristic> characteristics = string.IsNullOrEmpty(serviceUUIDRegex)
                ? Device.GetAllCharacteristics()
                : Device.GetAllCharacteristicsForServices(serviceUUIDRegex);

            // Check if any characteristics were found
            if (characteristics.Count < 1) return ValueTask.FromResult<IBluetoothLECharacteristic?>(null);

            // Check if the characteristic is not null
            IBluetoothLECharacteristic? characteristic = characteristics
                .FirstOrDefault(c => Regex.IsMatch(c.UUID, characteristicUUIDRegex));

            return ValueTask.FromResult(characteristic);
        }

        public override bool Connect(CancellationToken cancellationToken = default)
        {
            // Connect to hardware access (ensure discovery is started)
            HardwareAccess.Connect(cancellationToken);

            // Wait for free device to appear
            Device = HardwareAccess.ClaimDevice(cancellationToken);

            // Check if device was acquired correctly
            if (Device == null) return false;

            // Configure device as OnDeviceConnected won't be called
            _ConfigureDevice();

            return true;
        }

        public override bool Disconnect(CancellationToken cancellationToken = default)
        {
            // Prevent doing any operations on this device
            IsReady = false;
            
            // Check if device is null
            if (Device == null) return false;
            
            // Release device
            ReleaseDevice();
            return true;
        }

        private void ReleaseDevice()
        {
            // Check if device is null
            if (Device == null) return;
            
            // Device is not ready anymore (ensure proper value)
            IsReady = false;
            
            // Detach events and release device
            _DetachEvents();
            HardwareAccess.ReleaseDevice(Device);
            Device = null;
        }

        private void OnDeviceConnected(IBluetoothLEInterface sender, IBluetoothLEDevice device)
        {
            if (device != Device) return;

            // Device is connected, configure this device
            _ConfigureDevice();
        }

        private void OnDeviceDisconnected(IBluetoothLEInterface sender, IBluetoothLEDevice device)
        {
            if (device != Device) return;

            // Disconnect when device is disconnected
            Disconnect();
            IsReady = false;
        }
    }

    public abstract class BluetoothLowEnergyDeviceBaseInternal : DeviceBase<IBluetoothLEInterface>
    {
        // Constructor based on target Operating System
#if OS_WINDOWS
        internal BluetoothLowEnergyDeviceBaseInternal(IBluetoothLEAddress address) : this()
        {
            HardwareAccess = new WindowsBluetoothLEInterface(address);
        }
#elif OS_LINUX
        internal BluetoothLowEnergyDeviceBaseInternal(IBluetoothLEAddress address) : this() 
        {
            HardwareAccess = new LinuxBluetoothLEInterface(address);
        }
#endif
        // Constructor for internal use only
        internal BluetoothLowEnergyDeviceBaseInternal()
        {
            
        }
    }
}