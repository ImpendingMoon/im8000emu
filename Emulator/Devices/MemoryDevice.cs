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
			throw new ArgumentException("Device size is smaller than the data image.", nameof(size));
		}

		if (size == 0)
		{
			throw new ArgumentException("Size cannot be 0.", nameof(size));
		}

		_data = new byte[size];
		Array.Copy(data, _data, data.Length);
		_readOnly = readOnly;
	}

	public uint Size => (uint)_data.Length;

	public byte ReadByte(uint address)
	{
		uint offset = address % Size;
		return _data[offset];
	}

	public void WriteByte(uint address, byte value)
	{
		if (_readOnly)
		{
			return;
		}

		uint offset = address % Size;
		_data[offset] = value;
	}

	public Span<byte> ReadByteArray(uint address, uint length)
	{
		uint offset = address % Size;

		if (offset + length >= Size)
		{
			// Slow wrapped read
			byte[] data = new byte[length];
			for (int i = 0; i < length; i++)
			{
				data[i] = ReadByte((uint)(offset + i));
			}
			return data.AsSpan();
		}

		return _data.AsSpan()[(int)offset..(int)(offset + length)];
	}

	public void WriteByteArray(uint address, Span<byte> data)
	{
		if (_readOnly)
		{
			return;
		}

		uint offset = address % Size;

		if (offset + data.Length >= Size)
		{
			// Slow wrapped write
			for (int i = 0; i < data.Length; i++)
			{
				byte value = data[i];
				WriteByte((uint)(offset + i), value);
			}
		}
		else
		{
			data.CopyTo(_data.AsSpan()[(int)offset..]);
		}
	}
}
