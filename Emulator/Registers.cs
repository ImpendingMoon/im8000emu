namespace im8000emu.Emulator;

// The IM8000 uses Z80-like registers, but double the width (16/32-bit instead of 8/16-bit).
internal sealed class Registers
{
    public Registers()
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new NotSupportedException("This emulator core does not support big-endian architectures.");
        }
    }

    public byte GetRegisterByte(Constants.RegisterTargets reg, bool upperByte)
    {
        int position = _registerTargetToArrayPosition[reg];
        if (upperByte)
        {
            position += 1;
        }

        return _registerData[position];
    }

    public void SetRegisterByte(Constants.RegisterTargets reg, byte value, bool upperByte)
    {
        int position = _registerTargetToArrayPosition[reg];
        if (upperByte)
        {
            position += 1;
        }
        _registerData[position] = value;
    }

    public ushort GetRegisterWord(Constants.RegisterTargets reg)
    {
        int position = _registerTargetToArrayPosition[reg];
        return BitConverter.ToUInt16(_registerData, position);
    }

    public void SetRegisterWord(Constants.RegisterTargets reg, ushort value)
    {
        int position = _registerTargetToArrayPosition[reg];
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, _registerData, position, 2);
    }

    public uint GetRegisterDWord(Constants.RegisterTargets reg)
    {
        int position = _registerTargetToArrayPosition[reg];
        return BitConverter.ToUInt32(_registerData, position);
    }

    public void SetRegisterDWord(Constants.RegisterTargets reg, uint value)
    {
        int position = _registerTargetToArrayPosition[reg];
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, _registerData, position, 4);
    }

    public bool GetFlag(Constants.FlagMasks flag)
    {
        ushort flags = GetRegisterWord(Constants.RegisterTargets.F);
        return (flags & (ushort)flag) != 0;
    }

    public void SetFlag(Constants.FlagMasks flag, bool value)
    {
        ushort flags = GetRegisterWord(Constants.RegisterTargets.F);

        if (value)
        {
            flags |= (ushort)flag;
        }
        else
        {
            flags &= (ushort)~flag;
        }

        SetRegisterWord(Constants.RegisterTargets.F, flags);

        // Special handling of IFF2 for interrupts
        if (flag == Constants.FlagMasks.EnableInterrupts)
        {
            SetRegisterByte(Constants.RegisterTargets.IFF2, (byte)(value ? 1 : 0), false);
        }
    }

    // Used in RETN to restore interrupt enabled state
    public void RestoreIFF1()
    {
        byte iff2 = GetRegisterByte(Constants.RegisterTargets.IFF2, false);
        SetFlag(Constants.FlagMasks.EnableInterrupts, iff2 != 0);
    }

    public void ClearRegisters()
    {
        Array.Clear(_registerData);
    }

    // 14 32-bit registers + 32-bit PC + 32-bit I + 16-bit R + boolean IFF2 = 67 bytes
    private readonly byte[] _registerData = new byte[67];

    // Dict should _always_ map every enumeration in RegisterTargets.
    private static readonly Dictionary<Constants.RegisterTargets, int> _registerTargetToArrayPosition = new()
    {
        { Constants.RegisterTargets.F, 0 },
        { Constants.RegisterTargets.A, 2 },
        { Constants.RegisterTargets.C, 4 },
        { Constants.RegisterTargets.B, 6 },
        { Constants.RegisterTargets.E, 8 },
        { Constants.RegisterTargets.D, 10 },
        { Constants.RegisterTargets.L, 12 },
        { Constants.RegisterTargets.H, 14 },
        { Constants.RegisterTargets.IXL, 16 },
        { Constants.RegisterTargets.IXH, 18 },
        { Constants.RegisterTargets.IYL, 20 },
        { Constants.RegisterTargets.IYH, 22 },
        { Constants.RegisterTargets.SPL, 24 },
        { Constants.RegisterTargets.SPH, 26 },
        { Constants.RegisterTargets.F_, 28 },
        { Constants.RegisterTargets.A_, 30 },
        { Constants.RegisterTargets.C_, 32 },
        { Constants.RegisterTargets.B_, 34 },
        { Constants.RegisterTargets.E_, 36 },
        { Constants.RegisterTargets.D_, 38 },
        { Constants.RegisterTargets.L_, 40 },
        { Constants.RegisterTargets.H_, 42 },
        { Constants.RegisterTargets.IXL_, 44 },
        { Constants.RegisterTargets.IXH_, 46 },
        { Constants.RegisterTargets.IYL_, 48 },
        { Constants.RegisterTargets.IYH_, 50 },
        { Constants.RegisterTargets.SPL_, 52 },
        { Constants.RegisterTargets.SPH_, 54 },
        { Constants.RegisterTargets.AF, 0 },
        { Constants.RegisterTargets.BC, 4 },
        { Constants.RegisterTargets.DE, 8 },
        { Constants.RegisterTargets.HL, 12 },
        { Constants.RegisterTargets.IX, 16 },
        { Constants.RegisterTargets.IY, 20 },
        { Constants.RegisterTargets.SP, 24 },
        { Constants.RegisterTargets.AF_, 28 },
        { Constants.RegisterTargets.BC_, 32 },
        { Constants.RegisterTargets.DE_, 36 },
        { Constants.RegisterTargets.HL_, 40 },
        { Constants.RegisterTargets.IX_, 44 },
        { Constants.RegisterTargets.IY_, 48 },
        { Constants.RegisterTargets.SP_, 52 },
        { Constants.RegisterTargets.PC, 56 },
        { Constants.RegisterTargets.I, 60 },
        { Constants.RegisterTargets.R, 64 },
        { Constants.RegisterTargets.IFF2, 66 },
    };
}
