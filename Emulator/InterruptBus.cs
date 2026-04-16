using im8000emu.Emulator.Devices;

namespace im8000emu.Emulator;

internal class InterruptBus
{
	// Would use PriorityQueue, but doesn't implement IEnumerable
	private readonly List<(IInterruptingDevice device, int priority)> _devices = [];

	/// <summary>
	///     Attaches a device to the interrupt daisy-chain.
	/// </summary>
	/// <param name="priority">Priority in the daisy-chain. Lower values are higher priority.</param>
	/// <param name="device">Device to attach</param>
	/// <exception cref="ArgumentException">If an existing device already uses this priority.</exception>
	public void AttachDevice(IInterruptingDevice device, int priority)
	{
		for (int i = 0; i < _devices.Count; i++)
		{
			(_, int existingPriority) = _devices[i];

			if (existingPriority == priority)
			{
				throw new ArgumentException($"Cannot attach device, conflicting priority {priority}");
			}

			if (existingPriority > priority)
			{
				_devices.Insert(i, (device, priority));
				return;
			}
		}

		_devices.Add((device, priority));
	}

	public bool IsInterruptPending()
	{
		foreach ((IInterruptingDevice device, _) in _devices)
		{
			if (device.INT)
			{
				return true;
			}
		}

		return false;
	}

	public bool IsNonMaskableInterruptPending()
	{
		foreach ((IInterruptingDevice device, _) in _devices)
		{
			if (device.NMI)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	///     Called by the CPU to acknowledge an interrupt. Finds the highest-priority
	///     eligible device and lets it respond.
	/// </summary>
	/// <returns>The 8-bit interrupt number, or 0xFF if no device responds</returns>
	public byte AcknowledgeInterrupt()
	{
		for (int i = 0; i < _devices.Count; i++)
		{
			IInterruptingDevice device = _devices[i].device;

			if (device.INT && IsEligibleForInterrupt(i))
			{
				return device.OnInterruptAcknowledge();
			}
		}

		return 0xFF;
	}

	/// <summary>
	///     Called by the CPU to complete an interrupt (RETI/RETN).
	/// </summary>
	public void CompleteInterrupt()
	{
		foreach ((IInterruptingDevice device, _) in _devices)
		{
			if (device.IsServicingInterrupt)
			{
				device.OnInterruptComplete();
			}
		}
	}

	/// <summary>
	///     A device is eligible to be acknowledged only if no higher-priority device
	///     is currently servicing an interrupt.
	/// </summary>
	private bool IsEligibleForInterrupt(int deviceIndex)
	{
		for (int i = 0; i < deviceIndex; i++)
		{
			if (_devices[i].device.IsServicingInterrupt)
			{
				return false;
			}
		}

		return true;
	}
}
