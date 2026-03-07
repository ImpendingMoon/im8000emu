namespace im8000emu.Emulator;

internal class MemoryBus
{
    public MemoryBus()
    {
        Memory = [];
    }

    public MemoryBus(byte[] data, int memorySize)
    {
        if (memorySize < data.Length)
        {
            throw new ArgumentException("ROM image is larger than allocated Memory Size");
        }

        Memory = new byte[memorySize];
        Array.Copy(data, Memory, data.Length);
    }

    // Naive memory array. See commit 8eb002a2 for more complete implementation.
    // This is just enough to test the CPU implementation.
    public byte[] Memory { get; }

    public byte ReadByte(uint address)
    {
        if (address >= Memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "Address is out of bounds.");
        }

        return Memory[(int)address];
    }

    public void WriteByte(uint address, byte value)
    {
        if (address >= Memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "Address is out of bounds.");
        }

        Memory[(int)address] = value;
    }

    public Span<byte> ReadByteArray(uint address, uint length)
    {
        if (address + length > Memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "Address range is out of bounds.");
        }

        return Memory.AsSpan()[(int)address..(int)(address + length)];
    }

    public void WriteByteArray(uint address, Span<byte> data)
    {
        if (address + data.Length > Memory.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(address), "Address range is out of bounds.");
        }

        data.CopyTo(Memory.AsSpan()[(int)address..(int)(address + data.Length)]);
    }
}
