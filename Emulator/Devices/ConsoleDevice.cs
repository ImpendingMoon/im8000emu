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

	public ConsoleDevice()
	{
		Console.WriteLine("Virtual Console Device being used. Press Ctrl+D (EOF) to exit.");
	}

	public uint Read(uint address, Constants.DataSize _)
	{
		// Ignore size, we're a fully virtual device
		uint offset = address % Size;
		return offset switch
		{
			StatusOffset => BuildStatus(),
			RxDataOffset => ReadRx(),
			_ => 0xFF,
		};
	}

	public void Write(uint address, Constants.DataSize _, uint value)
	{
		uint offset = address % Size;

		if (offset == TxDataOffset)
		{
			_stdout.WriteByte((byte)value);
			_stdout.Flush();
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

		ConsoleKeyInfo key = Console.ReadKey(true);

		if (key.Key == ConsoleKey.D && key.Modifiers.HasFlag(ConsoleModifiers.Control))
		{
			Environment.Exit(1);
		}

		return (byte)key.KeyChar;
	}
}
