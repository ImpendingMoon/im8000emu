namespace im8000emu.Helpers;

internal class BitHelper
{
    public static uint SignExtend(uint value, int bits)
    {
        int shift = 32 - bits;
        return (uint)(((int)value << shift) >> shift);
    }

    public static bool IsParityEven(uint value)
    {
        int count = 0;
        for (int i = 0; i < 32; i++)
        {
            if ((value & (1 << i)) != 0)
            {
                count++;
            }
        }
        return (count % 2) == 0;
    }

    public static bool WillAdditionWrap(byte a, byte b)
    {
        return byte.MaxValue - a < b;
    }

    public static bool WillAdditionWrap(ushort a, ushort b)
    {
        return ushort.MaxValue - a < b;
    }

    public static bool WillAdditionWrap(uint a, uint b)
    {
        return uint.MaxValue - a < b;
    }

    public static bool WillAdditionOverflow(byte a, byte b)
    {
        byte sum = (byte)(a + b);
        return ((a ^ sum) & (b ^ sum) & 0x80) != 0;
    }

    public static bool WillAdditionOverflow(ushort a, ushort b)
    {
        ushort sum = (ushort)(a + b);
        return ((a ^ sum) & (b ^ sum) & 0x8000) != 0;
    }

    public static bool WillAdditionOverflow(uint a, uint b)
    {
        uint sum = (uint)(a + b);
        return ((a ^ sum) & (b ^ sum) & 0x80000000) != 0;
    }

    public static bool WillSubtractionWrap(uint a, uint b)
    {
        return a < b;
    }

    public static bool WillSubtractionUnderflow(byte a, byte b)
    {
        byte diff = (byte)(a + b);
        return ((a ^ b) & (a ^ diff) & 0x80) != 0;
    }

    public static bool WillSubtractionUnderflow(ushort a, ushort b)
    {
        ushort diff = (ushort)(a + b);
        return ((a ^ b) & (a ^ diff) & 0x8000) != 0;
    }

    public static bool WillSubtractionUnderflow(uint a, uint b)
    {
        uint diff = (uint)(a + b);
        return ((a ^ b) & (a ^ diff) & 0x80000000) != 0;
    }

    public static bool WillAdditionHalfCarry(byte a, byte b)
    {
        return (a & 0x0F) + (b & 0x0F) > 0x0F;
    }

    public static bool WillSubtractionHalfCarry(byte a, byte b)
    {
        return (a & 0x0F) < (b & 0x0F);
    }
}
