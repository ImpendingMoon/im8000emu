using im8000emu.Emulator.Devices;

namespace im8000emu.Emulator;

internal class MemoryBus
{
	private readonly List<Mapping> _mappings = [];
	private readonly int _waitStates;

	public MemoryBus(int waitStates)
	{
		_waitStates = waitStates;
	}

	public void AttachDevice(IMemoryDevice device, uint baseAddress, uint endAddress)
	{
		var mapping = new Mapping(device, baseAddress, endAddress);
		_mappings.Add(mapping);
	}

	public MemoryResult Read(uint address, Constants.DataSize size)
	{
		var result = new MemoryResult();
		bool aligned = (address & 1) == 0;

		foreach (Mapping mapping in _mappings)
		{
			if (mapping.ContainsAddress(address))
			{
				uint offset = address - mapping.StartAddress;
				try
				{
					result.Value = mapping.Device.Read(offset, size);
					result.Cycles = BusCycles(size, aligned);
					return result;
				}
				catch (DeviceException ex)
				{
					throw new MemoryBusException(address, ex.Size, ex.IsWrite, ex.Reason);
				}
			}
		}

		if (Config.EnableStrictMode)
		{
			throw new MemoryBusException(address, size, false, "unmapped address");
		}

		result.Value = 0xFFFFFFFF;
		return result;
	}

	public MemoryResult Write(uint address, Constants.DataSize size, uint value)
	{
		var result = new MemoryResult();
		bool aligned = (address & 1) == 0;

		foreach (Mapping mapping in _mappings)
		{
			if (mapping.ContainsAddress(address))
			{
				uint offset = address - mapping.StartAddress;
				try
				{
					mapping.Device.Write(offset, size, value);
					result.Cycles = BusCycles(size, aligned);
					return result;
				}
				catch (DeviceException ex)
				{
					throw new MemoryBusException(address, ex.Size, ex.IsWrite, ex.Reason);
				}
			}
		}

		if (Config.EnableStrictMode)
		{
			throw new MemoryBusException(address, size, true, "unmapped address");
		}

		return result;
	}

	private int BusCycles(Constants.DataSize size, bool aligned)
	{
		int cycles;

		if (Config.UseNarrowBus)
		{
			cycles = size switch
			{
				Constants.DataSize.Byte => 1,
				Constants.DataSize.Word => 2,
				Constants.DataSize.DWord => 4,
				_ => throw new EmulatorFaultException($"BusCycles: unhandled DataSize {size}"),
			};
		}
		else
		{
			cycles = size switch
			{
				Constants.DataSize.Byte => 1,
				Constants.DataSize.Word => aligned ? 1 : 2,
				Constants.DataSize.DWord => aligned ? 2 : 3,
				_ => throw new EmulatorFaultException($"BusCycles: unhandled DataSize {size}"),
			};
		}

		return cycles * (Config.BusCycleCost + _waitStates);
	}

	/// <summary>
	///     Thrown in here where we don't have CPU context
	/// </summary>
	internal sealed class MemoryBusException : Exception
	{
		public MemoryBusException(uint address, Constants.DataSize size, bool isWrite, string reason) : base(
			$"{(isWrite ? "write" : "read")} of {size} at 0x{address:X8}: {reason}"
		)
		{
			Address = address;
			Size = size;
			IsWrite = isWrite;
			Reason = reason;
		}

		public uint Address { get; }
		public Constants.DataSize Size { get; }
		public bool IsWrite { get; }
		public string Reason { get; }
	}

	private readonly struct Mapping
	{
		public Mapping(IMemoryDevice device, uint startAddress, uint endAddress)
		{
			Device = device;
			StartAddress = startAddress;
			EndAddress = endAddress;
		}

		public readonly IMemoryDevice Device;
		public readonly uint StartAddress;
		public readonly uint EndAddress;

		public bool ContainsAddress(uint address)
		{
			return address >= StartAddress && address <= EndAddress;
		}
	}
}
