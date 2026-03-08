using im8000emu.Emulator.Devices;

namespace im8000emu.Emulator;

internal class MemoryBus
{
	private readonly List<Mapping> _mappings = [];

	// ------------------------------------------------------------------
	// Device registration
	// ------------------------------------------------------------------

	/// <summary>
	///     Map a device into the address space starting at <paramref name="baseAddress" />.
	///     The device occupies [baseAddress, baseAddress + device.Size).
	///     Throws if the region overlaps an existing mapping.
	/// </summary>
	public void Map(uint baseAddress, IMemoryDevice device)
	{
		ArgumentNullException.ThrowIfNull(device);

		uint end = baseAddress + device.Size;

		foreach (Mapping m in _mappings)
		{
			if (baseAddress < m.End && end > m.Base)
			{
				throw new InvalidOperationException(
					$"Address range [0x{baseAddress:X}, 0x{end:X}) overlaps existing mapping " +
					$"[0x{m.Base:X}, 0x{m.End:X}) for {m.Device.GetType().Name}."
				);
			}
		}

		_mappings.Add(new Mapping(baseAddress, end, device));
	}

	public byte ReadByte(uint address)
	{
		(IMemoryDevice device, uint offset) = Resolve(address, 1);
		return device.ReadByte(offset);
	}

	public void WriteByte(uint address, byte value)
	{
		(IMemoryDevice device, uint offset) = Resolve(address, 1);
		device.WriteByte(offset, value);
	}

	public Span<byte> ReadByteArray(uint address, uint length)
	{
		(IMemoryDevice device, uint offset) = Resolve(address, length);
		return device.ReadByteArray(offset, length);
	}

	public void WriteByteArray(uint address, Span<byte> data)
	{
		(IMemoryDevice device, uint offset) = Resolve(address, (uint)data.Length);
		device.WriteByteArray(offset, data);
	}

	/// <summary>
	///     Find the device that owns <paramref name="address" /> and return it along
	///     with the device-local offset for that address.
	/// </summary>
	private (IMemoryDevice Device, uint Offset) Resolve(uint address, uint length)
	{
		foreach (Mapping m in _mappings)
		{
			if (address >= m.Base && address < m.End)
			{
				uint offset = address - m.Base;

				if ((offset + length) > m.Device.Size)
				{
					throw new ArgumentOutOfRangeException(
						nameof(address),
						$"Access [0x{address:X}, 0x{address + length:X}) crosses the boundary of " +
						$"{m.Device.GetType().Name} mapped at 0x{m.Base:X}."
					);
				}

				return (m.Device, offset);
			}
		}

		throw new ArgumentOutOfRangeException(nameof(address), $"No device mapped at address 0x{address:X}.");
	}

	private readonly record struct Mapping(uint Base, uint End, IMemoryDevice Device);
}
