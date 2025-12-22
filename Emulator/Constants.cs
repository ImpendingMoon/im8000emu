namespace im8000emu.Emulator;

internal class Constants
{
    public enum RegisterTargets
    {
        A, F, B, C, D, E, H, L, IXH, IXL, IYH, IYL, SPH, SPL,
        AF, BC, DE, HL, IX, IY, SP,
        A_, F_, B_, C_, D_, E_, H_, L_, IXL_, IXH_, IYL_, IYH_, SPL_, SPH_,
        AF_, BC_, DE_, HL_, IX_, IY_, SP_,
        PC, I, R,
        IFF2,
    }

    public enum FlagMasks : ushort
    {
        EnableInterrupts = 0b0000_0001_0000_0000,
        Sign = 0b1000_0000,
        Zero = 0b0100_0000,
        Unused5 = 0b0010_0000,
        HalfCarry = 0b0001_0000,
        Unused3 = 0b0000_1000,
        ParityOverflow = 0b0000_0100,
        Negative = 0b0000_0010,
        Carry = 0b0000_0001,
    }
}
