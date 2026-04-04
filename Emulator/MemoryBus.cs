using im8000emu.Emulator.Devices;

namespace im8000emu.Emulator;

internal class MemoryBus
{
	private readonly List<Mapping> _mappings = [];

	public void AttachDevice(IMemoryDevice device, uint baseAddress, uint endAddress)
	{
		var mapping = new Mapping(device, baseAddress, endAddress);
		_mappings.Add(mapping);
	}

	public uint Read(uint address, Constants.DataSize size)
	{
		foreach (Mapping mapping in _mappings)
		{
			if (mapping.ContainsAddress(address))
			{
				uint offset = address - mapping.StartAddress;
				try
				{
					return mapping.Device.Read(offset, size);
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

		return 0xFFFF_FFFF;
	}

	public void Write(uint address, Constants.DataSize size, uint value)
	{
		foreach (Mapping mapping in _mappings)
		{
			if (mapping.ContainsAddress(address))
			{
				uint offset = address - mapping.StartAddress;
				try
				{
					mapping.Device.Write(offset, size, value);
				}
				catch (DeviceException ex)
				{
					throw new MemoryBusException(address, ex.Size, ex.IsWrite, ex.Reason);
				}
				return;
			}
		}

		if (Config.EnableStrictMode)
		{
			throw new MemoryBusException(address, size, true, "unmapped address");
		}
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
