using System.Text;

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

    public uint GetRegister(Constants.RegisterTargets reg, Constants.OperandSize size)
    {
        int index = _registerTargetToArrayPosition[reg];

        return size switch
        {
            Constants.OperandSize.Byte => _registerData[index],
            Constants.OperandSize.Word => BitConverter.ToUInt16(_registerData, index),
            Constants.OperandSize.DWord => BitConverter.ToUInt32(_registerData, index),
            _ => throw new ArgumentException($"GetRegister does not support OperandSize {size}")
        };
    }

    public void SetRegister(Constants.RegisterTargets reg, Constants.OperandSize size, uint value)
    {
        int index = _registerTargetToArrayPosition[reg];

        switch (size)
        {
            case Constants.OperandSize.Byte:
            {
                _registerData[index] = (byte)value;
                break;
            }

            case Constants.OperandSize.Word:
            {
                int position = _registerTargetToArrayPosition[reg];
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Copy(bytes, 0, _registerData, position, 2);
                break;
            }

            case Constants.OperandSize.DWord:
            {
                int position = _registerTargetToArrayPosition[reg];
                byte[] bytes = BitConverter.GetBytes(value);
                Array.Copy(bytes, 0, _registerData, position, 4);
                break;
            }
        }
    }

    public bool GetFlag(Constants.FlagMasks flag)
    {
        ushort flags = (ushort)GetRegister(Constants.RegisterTargets.F, Constants.OperandSize.Word);
        return (flags & (ushort)flag) != 0;
    }

    public void SetFlag(Constants.FlagMasks flag, bool value)
    {
        ushort flags = (ushort)GetRegister(Constants.RegisterTargets.F, Constants.OperandSize.Word);

        if (value)
        {
            flags |= (ushort)flag;
        }
        else
        {
            flags &= (ushort)~flag;
        }

        SetRegister(Constants.RegisterTargets.F, Constants.OperandSize.Word, flags);

        // Special handling of IFF2 for interrupts
        if (flag == Constants.FlagMasks.EnableInterrupts)
        {
            SetRegister(Constants.RegisterTargets.IFF2, Constants.OperandSize.Byte, (byte)(value ? 1 : 0));
        }
    }

    // Used in RETN to restore interrupt enabled state
    public void RestoreIFF1()
    {
        uint iff2 = GetRegister(Constants.RegisterTargets.IFF2, Constants.OperandSize.Byte);
        SetFlag(Constants.FlagMasks.EnableInterrupts, iff2 != 0);
    }

    public void ClearRegisters()
    {
        Array.Clear(_registerData);
    }

    public string GetStandardDisplayString()
    {
        var sb = new StringBuilder();

        sb.Append($"AF: {GetRegister(Constants.RegisterTargets.AF, Constants.OperandSize.DWord):X8} ");
        sb.Append($"BC: {GetRegister(Constants.RegisterTargets.BC, Constants.OperandSize.DWord):X8} ");
        sb.Append($"DE: {GetRegister(Constants.RegisterTargets.DE, Constants.OperandSize.DWord):X8} ");
        sb.Append($"HL: {GetRegister(Constants.RegisterTargets.HL, Constants.OperandSize.DWord):X8} ");
        sb.Append($"IX: {GetRegister(Constants.RegisterTargets.IX, Constants.OperandSize.DWord):X8} ");
        sb.Append($"IY: {GetRegister(Constants.RegisterTargets.IY, Constants.OperandSize.DWord):X8} ");
        sb.Append($"SP: {GetRegister(Constants.RegisterTargets.SP, Constants.OperandSize.DWord):X8} ");
        sb.Append($"PC: {GetRegister(Constants.RegisterTargets.PC, Constants.OperandSize.DWord):X8} ");

        return sb.ToString();
    }

    public string GetAlternateDisplayString()
    {
        var sb = new StringBuilder();

        sb.Append($"AF': {GetRegister(Constants.RegisterTargets.AF_, Constants.OperandSize.DWord):X8} ");
        sb.Append($"BC': {GetRegister(Constants.RegisterTargets.BC_, Constants.OperandSize.DWord):X8} ");
        sb.Append($"DE': {GetRegister(Constants.RegisterTargets.DE_, Constants.OperandSize.DWord):X8} ");
        sb.Append($"HL': {GetRegister(Constants.RegisterTargets.HL_, Constants.OperandSize.DWord):X8} ");
        sb.Append($"IX': {GetRegister(Constants.RegisterTargets.IX_, Constants.OperandSize.DWord):X8} ");
        sb.Append($"IY': {GetRegister(Constants.RegisterTargets.IY_, Constants.OperandSize.DWord):X8} ");
        sb.Append($"SP': {GetRegister(Constants.RegisterTargets.SP_, Constants.OperandSize.DWord):X8} ");

        return sb.ToString();
    }

    public string GetSystemDisplayString()
    {
        var sb = new StringBuilder();

        sb.Append($"I: {GetRegister(Constants.RegisterTargets.I, Constants.OperandSize.DWord):X8} ");
        sb.Append($"R: {GetRegister(Constants.RegisterTargets.R, Constants.OperandSize.Word):X4} ");

        return sb.ToString();
    }

    public string GetFlagsDisplayString()
    {
        var sb = new StringBuilder();

        sb.Append($"C: {GetFlag(Constants.FlagMasks.Carry)} ");
        sb.Append($"N: {GetFlag(Constants.FlagMasks.Negative)} ");
        sb.Append($"PV: {GetFlag(Constants.FlagMasks.ParityOverflow)} ");
        sb.Append($"H: {GetFlag(Constants.FlagMasks.HalfCarry)} ");
        sb.Append($"Z: {GetFlag(Constants.FlagMasks.Zero)} ");
        sb.Append($"S: {GetFlag(Constants.FlagMasks.Sign)} ");
        sb.Append($"IE: {GetFlag(Constants.FlagMasks.EnableInterrupts)} ");
        sb.Append($"IFF2: {GetRegister(Constants.RegisterTargets.IFF2, Constants.OperandSize.Byte) == 1} ");

        return sb.ToString();
    }

    public string GetFullDisplayString()
    {
        var sb = new StringBuilder();

        sb.AppendLine("Registers:");
        sb.AppendLine(GetStandardDisplayString());
        sb.AppendLine("Alternate Registers:");
        sb.AppendLine(GetAlternateDisplayString());
        sb.AppendLine("System Registers:");
        sb.AppendLine(GetSystemDisplayString());
        sb.AppendLine("Flags:");
        sb.AppendLine(GetFlagsDisplayString());

        return sb.ToString();
    }

    public override string ToString()
    {
        return GetStandardDisplayString();
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
