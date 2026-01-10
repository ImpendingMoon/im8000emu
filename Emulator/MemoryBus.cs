namespace im8000emu.Emulator;

internal class MemoryBus
{
    public MemoryBus()
    {
        // Test program, calculate fibonacci sequence
        Memory = [
            0x00, 0xF8, 0x00, // LD.B L, 0
            0x00, 0xF4, 0x01, // LD.B H, 1
            0x00, 0xE5, 0x0A, 0x00, // LD.W B, 0
            0x00, 0xC1, // LD A, L
            0x08, 0xA1, // ADD A, H
            0x00, 0xB9, // LD L, H
            0x00, 0x15, // LD H, A
            0x1F, 0xF6, // DJNZ -10
            0x2A, 0xFE, 0xFC, 0xFF, // JR -4
        ];
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
