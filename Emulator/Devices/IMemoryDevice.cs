namespace im8000emu.Emulator.Devices;

internal interface IMemoryDevice
{
	uint Size { get; }

	uint Read(uint address, Constants.DataSize size);

	void Write(uint address, Constants.DataSize size, uint value);
}
