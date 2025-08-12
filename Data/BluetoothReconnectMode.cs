namespace IRIS.Bluetooth.Data
{
    /// <summary>
    ///     Enum used to determine type of device reconnection methodology.
    ///     Can be used to select device which is being reconnected after connection lost or
    ///     disable reconnection whatsoever.
    /// </summary>
    public enum BluetoothReconnectMode
    {
        /// <summary>
        ///     Device will be reconnected to same address as originally connected device
        ///     a.k.a. device must be same one.
        /// </summary>
        SameAddress,
        
        /// <summary>
        ///     Device will be reconnected to any device that has same name as originally connected one.
        /// </summary>
        SameName,
        
        /// <summary>
        ///     Device will be automatically reconnected to any device that meets interface requirements.
        ///     No other conditions are applied.
        /// </summary>
        AnySimilarDevice,
        
        /// <summary>
        ///     Device won't be reconnected when connection is lost.
        /// </summary>
        Disabled
    }
}