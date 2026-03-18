using im8000emu.Emulator.Devices;

namespace im8000emu.Emulator;

internal class InterruptBus
{
	private readonly PriorityQueue<IInterruptingDevice, int> _irqQueue = new();
	// Each entry is (priority, device). PriorityQueue dequeues smallest priority first.
	private readonly PriorityQueue<IInterruptingDevice, int> _nmiQueue = new();

	private readonly List<(int Priority, IInterruptingDevice Device)> _registrations = [];

	public bool HasPendingNmi
	{
		get
		{
			RebuildQueue(_nmiQueue, true);
			return _nmiQueue.Count > 0;
		}
	}

	public bool HasPendingInterrupt
	{
		get
		{
			RebuildQueue(_irqQueue, false);
			return _irqQueue.Count > 0;
		}
	}

	/// <summary>
	///     Registers a device on the bus with a given priority.
	///     Lower priority values are serviced first.
	///     Each device can only be registered once, and each priority value must be unique.
	/// </summary>
	public void RegisterDevice(IInterruptingDevice device, int priority)
	{
		if (_registrations.Any(r => r.Priority == priority))
		{
			throw new ArgumentException(
				$"Priority {priority} is already assigned to another device.",
				nameof(priority)
			);
		}

		_registrations.Add((priority, device));
	}

	/// <summary>
	///     Acknowledges the highest-priority pending interrupt of the requested class.
	///     Calls OnInterruptAcknowledge on the device, which is expected to return its interrupt number and
	///     de-assert its interrupt line. NMI interrupt numbers should be ignored by the CPU.
	///
	///		The CPU should always call HasPendingNmi/Interrupt before calling Acknowledge.
	/// </summary>
	/// <param name="nmi">
	///     True acknowledges the highest-priority pending NMI, else maskable interrupt.
	/// </param>
	/// <returns>
	///     The interrupt number
	/// </returns>
	public byte Acknowledge(bool nmi)
	{
		PriorityQueue<IInterruptingDevice, int> queue = nmi ? _nmiQueue : _irqQueue;

		if (!queue.TryDequeue(out IInterruptingDevice? device, out _))
		{
			throw new InvalidOperationException(
				nmi ? "No pending NMI to acknowledge." : "No pending maskable interrupt to acknowledge."
			);
		}

		return device.OnInterruptAcknowledged();
	}

	/// <summary>
	///     Clears and repopulates the interrupt queue with any device requesting an interrupt
	/// </summary>
	private void RebuildQueue(PriorityQueue<IInterruptingDevice, int> queue, bool nmi)
	{
		queue.Clear();

		foreach ((int priority, IInterruptingDevice device) in _registrations)
		{
			bool raised = nmi ? device.RaisedNonMaskableInterrupt : device.RaisedInterrupt;
			if (raised)
			{
				queue.Enqueue(device, priority);
			}
		}
	}
}
