namespace im8000emu.Emulator.Devices;

internal interface IMemoryDevice
{
	uint Size { get; }

	byte ReadByte(uint offset);

	void WriteByte(uint offset, byte value);

	Span<byte> ReadByteArray(uint offset, uint length);

	void WriteByteArray(uint offset, Span<byte> data);
}
