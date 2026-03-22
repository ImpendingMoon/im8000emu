using System.Buffers.Binary;

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

		var rand = new Random();

		for (uint i = 0; i < _data.Length; i++)
		{
			_data[i] = (byte)rand.Next();
		}
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

	public uint Read(uint address, Constants.DataSize size)
	{
		uint offset = address % Size;

		return size switch
		{
			Constants.DataSize.Byte => _data[offset],

			Constants.DataSize.Word => (offset + 2) <= Size
				? BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan((int)offset))
				: (uint)(_data[offset % Size] | (_data[(offset + 1) % Size] << 8)),

			Constants.DataSize.DWord => (offset + 4) <= Size
				? BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan((int)offset))
				: (uint)(_data[offset % Size] |
					(_data[(offset + 1) % Size] << 8) |
					(_data[(offset + 2) % Size] << 16) |
					(_data[(offset + 3) % Size] << 24)),

			_ => throw new ArgumentOutOfRangeException(nameof(size)),
		};
	}

	public void Write(uint address, Constants.DataSize size, uint value)
	{
		if (_readOnly)
		{
			return;
		}

		uint offset = address % Size;

		switch (size)
		{
			case Constants.DataSize.Byte:
				_data[offset] = (byte)value;
				break;

			case Constants.DataSize.Word:
				if ((offset + 2) <= Size)
				{
					BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan((int)offset), (ushort)value);
				}
				else
				{
					_data[offset % Size] = (byte)value;
					_data[(offset + 1) % Size] = (byte)(value >> 8);
				}
				break;

			case Constants.DataSize.DWord:
				if ((offset + 4) <= Size)
				{
					BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan((int)offset), value);
				}
				else
				{
					_data[offset % Size] = (byte)value;
					_data[(offset + 1) % Size] = (byte)(value >> 8);
					_data[(offset + 2) % Size] = (byte)(value >> 16);
					_data[(offset + 3) % Size] = (byte)(value >> 24);
				}
				break;
		}
	}
}
