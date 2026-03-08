namespace im8000emu.Emulator.Devices;

/// <summary>
///     A flat byte-array memory device. Can be configured as read-only (ROM) or read-write (RAM).
/// </summary>
internal class MemoryDevice : IMemoryDevice
{
	private readonly byte[] _data;
	private readonly bool _readOnly;

	public MemoryDevice(uint size, bool readOnly = false)
	{
		_data = new byte[size];
		_readOnly = readOnly;
	}

	public MemoryDevice(byte[] data, int size, bool readOnly = false)
	{
		if (size < data.Length)
		{
			throw new ArgumentException("Provided size is smaller than the data array.", nameof(size));
		}

		_data = new byte[size];
		Array.Copy(data, _data, data.Length);
		_readOnly = readOnly;
	}

	public uint Size => (uint)_data.Length;

	public byte ReadByte(uint offset)
	{
		CheckBounds(offset, 1);
		return _data[offset];
	}

	public void WriteByte(uint offset, byte value)
	{
		CheckBounds(offset, 1);
		if (_readOnly)
		{
			throw new InvalidOperationException($"Write to read-only MemoryDevice at offset 0x{offset:X}.");
		}

		_data[offset] = value;
	}

	public Span<byte> ReadByteArray(uint offset, uint length)
	{
		CheckBounds(offset, length);
		return _data.AsSpan()[(int)offset..(int)(offset + length)];
	}

	public void WriteByteArray(uint offset, Span<byte> data)
	{
		CheckBounds(offset, (uint)data.Length);
		if (_readOnly)
		{
			throw new InvalidOperationException($"Write to read-only MemoryDevice at offset 0x{offset:X}.");
		}

		data.CopyTo(_data.AsSpan()[(int)offset..]);
	}

	private void CheckBounds(uint offset, uint length)
	{
		if ((offset + length) > Size)
		{
			throw new ArgumentOutOfRangeException(
				nameof(offset),
				$"Access 0x{offset:X}+{length} is out of bounds for device of size 0x{Size:X}."
			);
		}
	}
}
