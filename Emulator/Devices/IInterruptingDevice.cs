namespace im8000emu.Emulator.Devices;

internal interface IInterruptingDevice
{
	/// <summary>Whether the device is currently asserting the maskable interrupt (INT) line.</summary>
	bool RaisedInterrupt { get; }

	/// <summary>Whether the device is currently asserting the non-maskable interrupt (NMI) line.</summary>
	bool RaisedNonMaskableInterrupt { get; }

	/// <summary>
	///     Called when this device's interrupt is acknowledged.
	///     The device should de-assert its interrupt and return an interrupt number.
	/// </summary>
	/// <returns>
	///     The interrupt number for the currently asserted interrupt
	/// </returns>
	byte OnInterruptAcknowledged();
}
