namespace im8000emu.Emulator.Devices;

/// <summary>
///     Memory-mapped console device using stdin/stdout.
///     Address map:
///     0 - STATUS (read)
///     - b0: RX_READY
///     - b1: TX_READY
///     1 - RX_DATA (read)
///     2 - TX_DATA (write)
/// </summary>
internal class ConsoleDevice : IMemoryDevice
{
	public const uint OffsetStatus = 0;
	public const uint OffsetRxData = 1;
	public const uint OffsetTxData = 2;

	public const byte StatusRxReady = 0b0000_0001;
	public const byte StatusTxReady = 0b0000_0010;

	private readonly Stream _stdout = Console.OpenStandardOutput();

	public uint Size => 3;

	public byte ReadByte(uint offset)
	{
		return offset switch
		{
			OffsetStatus => BuildStatus(),
			OffsetRxData => ReadRx(),
			OffsetTxData => throw new InvalidOperationException("TX_DATA is write-only."),
			_ => throw new ArgumentOutOfRangeException(nameof(offset)),
		};
	}

	public void WriteByte(uint offset, byte value)
	{
		switch (offset)
		{
			case OffsetTxData:
			{
				_stdout.WriteByte(value);
				_stdout.Flush();
				break;
			}

			case OffsetStatus: throw new InvalidOperationException("STATUS is read-only");
			case OffsetRxData: throw new InvalidOperationException("RX_DATA is read-only.");
			default: throw new ArgumentOutOfRangeException(nameof(offset));
		}
	}

	public Span<byte> ReadByteArray(uint offset, uint length)
	{
		byte[] result = new byte[length];
		for (uint i = 0; i < length; i++)
		{
			result[i] = ReadByte(offset + i);
		}
		return result;
	}

	public void WriteByteArray(uint offset, Span<byte> data)
	{
		for (int i = 0; i < data.Length; i++)
		{
			WriteByte(offset + (uint)i, data[i]);
		}
	}

	private static byte BuildStatus()
	{
		byte status = StatusTxReady;

		if (Console.KeyAvailable)
		{
			status |= StatusRxReady;
		}

		return status;
	}

	private static byte ReadRx()
	{
		if (!Console.KeyAvailable)
		{
			return 0x00;
		}

		return (byte)Console.ReadKey(true).KeyChar;
	}
}
