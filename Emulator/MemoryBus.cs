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

	public byte ReadByte(uint address)
	{
		foreach (Mapping mapping in _mappings)
		{
			if (mapping.ContainsAddress(address))
			{
				uint offset = address - mapping.StartAddress;
				return mapping.Device.ReadByte(offset);
			}
		}
		return 0xFF;
	}

	public void WriteByte(uint address, byte value)
	{
		foreach (Mapping mapping in _mappings)
		{
			if (mapping.ContainsAddress(address))
			{
				uint offset = address - mapping.StartAddress;
				mapping.Device.WriteByte(offset, value);
			}
		}
	}

	public Span<byte> ReadByteArray(uint address, uint length)
	{
		foreach (Mapping mapping in _mappings)
		{
			if (mapping.ContainsAddress(address))
			{
				uint offset = address - mapping.StartAddress;
				return mapping.Device.ReadByteArray(offset, length);
			}
		}

		// Slow, but should be rare in well-behaved software
		byte[] openBus = new byte[length];
		Array.Fill<byte>(openBus, 0xFF);
		return new Span<byte>(openBus);
	}

	public void WriteByteArray(uint address, Span<byte> data)
	{
		foreach (Mapping mapping in _mappings)
		{
			if (mapping.ContainsAddress(address))
			{
				uint offset = address - mapping.StartAddress;
				mapping.Device.WriteByteArray(offset, data);
			}
		}
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
