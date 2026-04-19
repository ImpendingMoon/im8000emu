namespace im8000emu.Emulator.Devices;

/// <summary>
///     Zilog Z80 CTC.
///     Four independently programmable counter/timer channels.
///     Address map (CS1:CS0 = address bits 1:0):
///     0x00 - Channel 0
///     0x01 - Channel 1
///     0x02 - Channel 2
///     0x03 - Channel 3
///     Each channel is programmed with:
///     1. A control word (b0=1), or an interrupt vector word (b0=0, channel 0 only)
///     2. A time constant word (0x01-0xFF, where 0x00 means 256)
///     Control word bits:
///     b7 - Interrupt Enable (1 = enabled)
///     b6 - Mode (0 = timer, 1 = counter)
///     b5 - Prescaler (0 = 16, 1 = 256)
///     b4 - CLK/TRG Edge (0 = falling, 1 = rising) (unused)
///     b3 - Timer Trigger (0=automatic, 1=CLK/TRG)
///     b2 - Time Constant Follows
///     b1 - Software Reset (1 = reset this channel)
///     b0 - Control/Vector (1 = control word, 0 = vector word)
/// </summary>
public class CTCDevice : IMemoryDevice, ISteppingDevice, IInterruptingDevice
{
	private const int ChannelCount = 4;

	private readonly Channel[] _channels = new Channel[ChannelCount];

	// The top 5 bits of the interrupt vector, programmed via a vector word to channel 0.
	// The CTC fills in bits 3:1 with the channel number and forces bit 0 to 0.
	private byte _interruptVectorBase;

	public CTCDevice()
	{
		for (int i = 0; i < ChannelCount; i++)
		{
			_channels[i] = new Channel(i);
		}
	}

	public bool INT => _channels.Any(x => x.InterruptPending);

	public bool NMI => false;

	public bool IsServicingInterrupt => _channels.Any(x => x.IsServicingInterrupt);

	public byte OnInterruptAcknowledge()
	{
		foreach (Channel ch in _channels)
		{
			if (ch.InterruptPending)
			{
				ch.Acknowledge();
				// Vector: top 5 bits from base, bits 1-2 = channel number, bit 0 = 0
				return (byte)((_interruptVectorBase & 0b11111000) | (ch.Index << 1));
			}
		}

		return 0xFF;
	}

	public void OnInterruptComplete()
	{
		foreach (Channel ch in _channels)
		{
			if (ch.IsServicingInterrupt)
			{
				ch.CompleteService();
				break;
			}
		}
	}

	public uint Size => 4;

	/// <summary>
	///     Read returns the current counter value for the given channel.
	/// </summary>
	public uint Read(uint address, Constants.DataSize size)
	{
		uint offset = address % Size;

		if (size != Constants.DataSize.Byte)
		{
			if (Config.EnableStrictMode)
			{
				throw new DeviceException(offset, size, false, $"cannot access CTC with {size} size");
			}

			if (size == Constants.DataSize.Word)
			{
				uint value = Read(offset, Constants.DataSize.Byte);
				value |= Read((offset + 1) % Size, Constants.DataSize.Byte) << 8;
				return value;
			}
			else
			{
				uint value = Read(offset, Constants.DataSize.Byte);
				value |= Read((offset + 1) % Size, Constants.DataSize.Byte) << 8;
				value |= Read((offset + 2) % Size, Constants.DataSize.Byte) << 16;
				value |= Read((offset + 3) % Size, Constants.DataSize.Byte) << 24;
				return value;
			}
		}

		return _channels[offset].ReadCounter();
	}

	/// <summary>
	///     Writes a control word, interrupt vector, or time constant to the given channel.
	/// </summary>
	public void Write(uint address, Constants.DataSize size, uint value)
	{
		uint offset = address % Size;

		if (size != Constants.DataSize.Byte)
		{
			if (Config.EnableStrictMode)
			{
				throw new DeviceException(offset, size, true, $"cannot access CTC with {size} size");
			}

			if (size == Constants.DataSize.Word)
			{
				Write(offset, Constants.DataSize.Byte, value & 0xFF);
				Write((offset + 1) % Size, Constants.DataSize.Byte, (value >> 8) & 0xFF);
			}
			else
			{
				Write(offset, Constants.DataSize.Byte, value & 0xFF);
				Write((offset + 1) % Size, Constants.DataSize.Byte, (value >> 8) & 0xFF);
				Write((offset + 2) % Size, Constants.DataSize.Byte, (value >> 16) & 0xFF);
				Write((offset + 3) % Size, Constants.DataSize.Byte, (value >> 24) & 0xFF);
			}

			return;
		}

		byte b = (byte)(value & 0xFF);
		Channel ch = _channels[offset];

		if (ch.ExpectingTimeConstant)
		{
			ch.LoadTimeConstant(b);
		}
		else if ((b & 0x01) == 0)
		{
			// b0 = 0: interrupt vector word, always directed to channel 0 regardless of address
			_interruptVectorBase = b;
		}
		else
		{
			// b0=1: channel control word
			ch.WriteControlWord(b);
		}
	}

	public int Step(int cycles)
	{
		foreach (Channel ch in _channels)
		{
			ch.Step(cycles);
		}

		return 0;
	}

	/// <summary>
	///     Sends an external CLK/TRG pulse to the specified channel.
	///     In counter mode this decrements the down-counter. In timer mode with
	///     external trigger it starts the timer.
	///     Call this from whatever device is wired to the CTC's CLK/TRG pins.
	/// </summary>
	public void ClockChannel(int channelIndex)
	{
		if (channelIndex < 0 || channelIndex >= ChannelCount)
		{
			return;
		}

		_channels[channelIndex].ExternalClock();
	}

	private sealed class Channel
	{
		private bool _active; // actively counting/timing
		private bool _counterMode; // false = timer, true = counter
		private byte _downCounter; // current count value
		private bool _enableInterrupts;
		private bool _externalTrigger; // false = auto, true = CLK/TRG starts
		private bool _hasTimeConstant;
		private bool _prescaler256; // false = /16, true = /256
		private int _prescalerCount; // cycles accumulated for next prescaler tick
		private byte _timeConstant; // 0-255 (0 = 256)
		private bool _triggerRisingEdge; // edge selection for CLK/TRG (unused)

		public Channel(int index)
		{
			Index = index;
			HardwareReset();
		}

		public bool InterruptPending { get; private set; }
		public bool IsServicingInterrupt { get; private set; }

		public int Index { get; }

		public bool ExpectingTimeConstant { get; private set; }

		public void WriteControlWord(byte b)
		{
			// b1 = 1, software reset
			if ((b & 0x02) != 0)
			{
				SoftwareReset();
			}

			_enableInterrupts = (b & 0x80) != 0;
			_counterMode = (b & 0x40) != 0;
			_prescaler256 = (b & 0x20) != 0;

			// b4, edge selection
			bool newRisingEdge = (b & 0x10) != 0;
			// If changed while running, acts as an extra trigger
			bool edgeChanged = newRisingEdge != _triggerRisingEdge && _hasTimeConstant && !_active;
			_triggerRisingEdge = newRisingEdge;

			_externalTrigger = (b & 0x08) != 0;

			ExpectingTimeConstant = (b & 0x04) != 0;

			// If not expecting a time constant and the channel has one, we can treat
			// a control-word-only update (b2 = 0) as continuing. Nothing to do here
			// since the counter keeps running.

			if (!_enableInterrupts)
			{
				InterruptPending = false;
			}

			// Edge-change in timer mode while pending start acts as a CLK/TRG pulse
			if (edgeChanged && !_counterMode && !_active && _hasTimeConstant)
			{
				StartTimer();
			}
		}

		public void LoadTimeConstant(byte tc)
		{
			_timeConstant = tc;
			_hasTimeConstant = true;
			ExpectingTimeConstant = false;

			// If the channel is already running, the new constant is loaded
			// at the next zero count (Reload will pick it up).
			// If not running, load immediately and start.
			if (!_active)
			{
				ReloadCounter();

				if (_counterMode)
				{
					// Counter mode: waits for external CLK/TRG pulses.
					// Mark active so reads return the counter value but don't auto-decrement
					_active = true;
				}
				else if (!_externalTrigger)
				{
					// Timer mode, automatic trigger
					StartTimer();
				}
				// else: timer mode, external trigger. Stays inactive until CLK/TRG
			}
		}

		/// <summary>Advance the channel by CPU cycles (timer mode only).</summary>
		public void Step(int cycles)
		{
			if (!_active || _counterMode)
			{
				return;
			}

			int prescalerDivisor = _prescaler256 ? 256 : 16;
			_prescalerCount += cycles;

			while (_prescalerCount >= prescalerDivisor)
			{
				_prescalerCount -= prescalerDivisor;
				Decrement();
			}
		}

		/// <summary>
		///     External CLK/TRG pulse. In counter mode, decrements.
		///     In timer mode with external trigger, starts the timer.
		/// </summary>
		public void ExternalClock()
		{
			if (_counterMode)
			{
				if (_active)
				{
					Decrement();
				}
			}
			else if (!_active && _hasTimeConstant && _externalTrigger)
			{
				StartTimer();
			}
		}

		/// <summary>Returns the current down-counter value (0x00 in flight means 256).</summary>
		public byte ReadCounter()
		{
			return _active ? _downCounter : _timeConstant;
		}

		public void Acknowledge()
		{
			InterruptPending = false;
			IsServicingInterrupt = true;
		}

		public void CompleteService()
		{
			IsServicingInterrupt = false;
		}

		private void StartTimer()
		{
			_active = true;
			_prescalerCount = 0;
			// Counter is already loaded with the time constant by LoadTimeConstant/Reload
		}

		private void SoftwareReset()
		{
			_active = false;
			_prescalerCount = 0;
			InterruptPending = false;
			// IsServicingInterrupt is not cleared
			// ExpectingTimeConstant will be set based on b2 of the control word
		}

		private void HardwareReset()
		{
			_active = false;
			_prescalerCount = 0;
			_hasTimeConstant = false;
			_enableInterrupts = false;
			InterruptPending = false;
			IsServicingInterrupt = false;
			ExpectingTimeConstant = false;
		}

		private void ReloadCounter()
		{
			_downCounter = _timeConstant;
		}

		private void Decrement()
		{
			_downCounter--;

			if (_downCounter == 0)
			{
				if (_enableInterrupts)
				{
					InterruptPending = true;
				}

				ReloadCounter();
			}
		}
	}
}
