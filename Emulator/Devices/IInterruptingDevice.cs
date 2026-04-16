namespace im8000emu.Emulator.Devices;

/// <summary>
///     Represents a device on the Z80 IM 2 interrupt bus
/// </summary>
internal interface IInterruptingDevice
{
	/// <summary>
	///     Interrupt request line. When true, signals to the CPU that a maskable
	///     interrupt is ready.
	/// </summary>
	bool INT { get; }

	/// <summary>
	///     Non-maskable interrupt request line. When true, signals to the CPU that
	///     a non-maskable interrupt is ready.
	/// </summary>
	bool NMI { get; }

	/// <summary>
	///     True while this device is currently servicing an interrupt (after
	///     acknowledge and before complete). The bus uses this to block lower-priority
	///     devices from being acknowledged.
	/// </summary>
	bool IsServicingInterrupt { get; }

	/// <summary>
	///     Called when the CPU acknowledges an interrupt. The device should
	///     de-assert INT and set IsServicingInterrupt to true.
	///     If asserting NMI, do not set IsServicingInterrupt, as no Complete
	///     signal is sent on RETN
	/// </summary>
	/// <returns>The 8-bit interrupt number</returns>
	byte OnInterruptAcknowledge();

	/// <summary>
	///     Called when the CPU finishes servicing an interrupt (via RETI).
	///     The device should set IsServicingInterrupt to false.
	/// </summary>
	void OnInterruptComplete();
}
