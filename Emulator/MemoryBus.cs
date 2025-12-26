namespace im8000emu.Emulator
{
    internal class MemoryBus
    {
        public MemoryBus()
        {
        }

        public List<IMemoryBusDevice> Devices { get; } = [];

        public byte ReadByte(uint address)
        {
            // Placeholder implementation
            return 0x00;
        }

        public void WriteByte(uint address, byte value)
        {
            // Placeholder implementation
        }

        public Span<byte> ReadByteArray(uint address, int length)
        {
            // Placeholder implementation
            return new byte[length];
        }

        public void WriteByteArray(uint address, Span<byte> data)
        {
            // Placeholder implementation
        }
    }
}
