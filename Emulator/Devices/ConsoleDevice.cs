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
	public const uint StatusOffset = 0;
	public const uint RxDataOffset = 1;
	public const uint TxDataOffset = 2;

	public const byte StatusRxReadyBit = 0b0000_0001;
	public const byte StatusTxReadyBit = 0b0000_0010;

	private readonly Stream _stdout = Console.OpenStandardOutput();

	public uint Size => 4;

	public byte ReadByte(uint address)
	{
		uint offset = address % Size;
		return offset switch
		{
			StatusOffset => BuildStatus(),
			RxDataOffset => ReadRx(),
			_ => 0xFF,
		};
	}

	public void WriteByte(uint address, byte value)
	{
		uint offset = address % Size;

		if (offset == TxDataOffset)
		{
			_stdout.WriteByte(value);
			_stdout.Flush();
		}
	}

	public Span<byte> ReadByteArray(uint address, uint length)
	{
		byte[] result = new byte[length];
		for (uint i = 0; i < length; i++)
		{
			result[i] = ReadByte(address + i);
		}
		return result;
	}

	public void WriteByteArray(uint address, Span<byte> data)
	{
		for (int i = 0; i < data.Length; i++)
		{
			WriteByte(address + (uint)i, data[i]);
		}
	}

	private static byte BuildStatus()
	{
		byte status = StatusTxReadyBit;

		if (Console.KeyAvailable)
		{
			status |= StatusRxReadyBit;
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
