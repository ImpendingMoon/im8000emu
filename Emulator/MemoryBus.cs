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
				return mapping.Device.Read(offset, size);
			}
		}

		if (Config.EnableStrictMode)
		{
			throw new MemoryAccessException(address, size, false);
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
				mapping.Device.Write(offset, size, value);
				return;
			}
		}

		if (Config.EnableStrictMode)
		{
			throw new MemoryAccessException(address, size, true);
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
