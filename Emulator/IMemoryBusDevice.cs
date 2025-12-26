namespace im8000emu.Emulator
{
    internal interface IMemoryBusDevice
    {
        public byte ReadByte(uint address);
        public void WriteByte(uint address, byte value);
    }
}
