namespace im8000emu.Emulator.Devices;

internal interface IMemoryDevice
{
	uint Size { get; }

	byte ReadByte(uint address);

	void WriteByte(uint address, byte value);

	Span<byte> ReadByteArray(uint address, uint length);

	void WriteByteArray(uint address, Span<byte> data);
}
