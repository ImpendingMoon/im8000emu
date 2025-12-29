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

    public static bool WillAdditionOverflow(byte a, byte b)
    {
        return byte.MaxValue - a < b;
    }

    public static bool WillAdditionOverflow(ushort a, ushort b)
    {
        return ushort.MaxValue - a < b;
    }

    public static bool WillAdditionOverflow(uint a, uint b)
    {
        return uint.MaxValue - a < b;
    }

    public static bool WillSubtractionUnderflow(uint a, uint b)
    {
        return a < b;
    }
}
