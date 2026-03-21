using System.Buffers.Binary;
using System.Text;

namespace im8000emu.Emulator;

// The IM8000 uses Z80-like registers, but double the width (16/32-bit instead of 8/16-bit).
internal sealed class Registers
{
	private static readonly int[] _registerOffsets;

	// 14 32-bit registers + 32-bit PC + 32-bit I + 16-bit R = 66 bytes
	private readonly byte[] _registerData = new byte[66];

	static Registers()
	{
		int count = (int)Constants.RegisterTargets.R + 1;
		_registerOffsets = new int[count];

		_registerOffsets[(int)Constants.RegisterTargets.F] = 0;
		_registerOffsets[(int)Constants.RegisterTargets.A] = 2;
		_registerOffsets[(int)Constants.RegisterTargets.C] = 4;
		_registerOffsets[(int)Constants.RegisterTargets.B] = 6;
		_registerOffsets[(int)Constants.RegisterTargets.E] = 8;
		_registerOffsets[(int)Constants.RegisterTargets.D] = 10;
		_registerOffsets[(int)Constants.RegisterTargets.L] = 12;
		_registerOffsets[(int)Constants.RegisterTargets.H] = 14;
		_registerOffsets[(int)Constants.RegisterTargets.IXL] = 16;
		_registerOffsets[(int)Constants.RegisterTargets.IXH] = 18;
		_registerOffsets[(int)Constants.RegisterTargets.IYL] = 20;
		_registerOffsets[(int)Constants.RegisterTargets.IYH] = 22;
		_registerOffsets[(int)Constants.RegisterTargets.SPL] = 24;
		_registerOffsets[(int)Constants.RegisterTargets.SPH] = 26;

		_registerOffsets[(int)Constants.RegisterTargets.F_] = 28;
		_registerOffsets[(int)Constants.RegisterTargets.A_] = 30;
		_registerOffsets[(int)Constants.RegisterTargets.C_] = 32;
		_registerOffsets[(int)Constants.RegisterTargets.B_] = 34;
		_registerOffsets[(int)Constants.RegisterTargets.E_] = 36;
		_registerOffsets[(int)Constants.RegisterTargets.D_] = 38;
		_registerOffsets[(int)Constants.RegisterTargets.L_] = 40;
		_registerOffsets[(int)Constants.RegisterTargets.H_] = 42;
		_registerOffsets[(int)Constants.RegisterTargets.IXL_] = 44;
		_registerOffsets[(int)Constants.RegisterTargets.IXH_] = 46;
		_registerOffsets[(int)Constants.RegisterTargets.IYL_] = 48;
		_registerOffsets[(int)Constants.RegisterTargets.IYH_] = 50;
		_registerOffsets[(int)Constants.RegisterTargets.SPL_] = 52;
		_registerOffsets[(int)Constants.RegisterTargets.SPH_] = 54;

		// 32-bit register views (aliased into the same byte positions as their halves)
		_registerOffsets[(int)Constants.RegisterTargets.AF] = 0;
		_registerOffsets[(int)Constants.RegisterTargets.BC] = 4;
		_registerOffsets[(int)Constants.RegisterTargets.DE] = 8;
		_registerOffsets[(int)Constants.RegisterTargets.HL] = 12;
		_registerOffsets[(int)Constants.RegisterTargets.IX] = 16;
		_registerOffsets[(int)Constants.RegisterTargets.IY] = 20;
		_registerOffsets[(int)Constants.RegisterTargets.SP] = 24;

		_registerOffsets[(int)Constants.RegisterTargets.AF_] = 28;
		_registerOffsets[(int)Constants.RegisterTargets.BC_] = 32;
		_registerOffsets[(int)Constants.RegisterTargets.DE_] = 36;
		_registerOffsets[(int)Constants.RegisterTargets.HL_] = 40;
		_registerOffsets[(int)Constants.RegisterTargets.IX_] = 44;
		_registerOffsets[(int)Constants.RegisterTargets.IY_] = 48;
		_registerOffsets[(int)Constants.RegisterTargets.SP_] = 52;

		_registerOffsets[(int)Constants.RegisterTargets.PC] = 56;
		_registerOffsets[(int)Constants.RegisterTargets.I] = 60;
		_registerOffsets[(int)Constants.RegisterTargets.R] = 64;
	}

	public uint GetRegister(Constants.RegisterTargets reg, Constants.DataSize size)
	{
		int i = _registerOffsets[(int)reg];

		return size switch
		{
			Constants.DataSize.Byte => _registerData[i],
			Constants.DataSize.Word => BinaryPrimitives.ReadUInt16LittleEndian(_registerData.AsSpan(i)),
			Constants.DataSize.DWord => BinaryPrimitives.ReadUInt32LittleEndian(_registerData.AsSpan(i)),
			_ => throw new ArgumentException($"GetRegister does not support DataSize {size}"),
		};
	}

	public void SetRegister(Constants.RegisterTargets reg, Constants.DataSize size, uint value)
	{
		int i = _registerOffsets[(int)reg];

		switch (size)
		{
			case Constants.DataSize.Byte:
			{
				_registerData[i] = (byte)value;
				break;
			}

			case Constants.DataSize.Word:
			{
				BinaryPrimitives.WriteUInt16LittleEndian(_registerData.AsSpan(i), (ushort)value);
				break;
			}

			case Constants.DataSize.DWord:
			{
				BinaryPrimitives.WriteUInt32LittleEndian(_registerData.AsSpan(i), value);
				break;
			}
		}
	}

	public bool GetFlag(Constants.FlagMasks flag)
	{
		uint flags = BinaryPrimitives.ReadUInt16LittleEndian(_registerData.AsSpan(0));
		return (flags & (uint)flag) != 0;
	}

	public void SetFlag(Constants.FlagMasks flag, bool value)
	{
		uint flags = BinaryPrimitives.ReadUInt16LittleEndian(_registerData.AsSpan(0));

		if (value)
		{
			flags |= (uint)flag;
		}
		else
		{
			flags &= ~(uint)flag;
		}

		BinaryPrimitives.WriteUInt16LittleEndian(_registerData.AsSpan(0), (ushort)flags);
	}

	public void ClearRegisters()
	{
		Array.Clear(_registerData);
	}

	public string GetStandardDisplayString()
	{
		var sb = new StringBuilder();

		sb.Append($"AF: {GetRegister(Constants.RegisterTargets.AF, Constants.DataSize.DWord):X8} ");
		sb.Append($"BC: {GetRegister(Constants.RegisterTargets.BC, Constants.DataSize.DWord):X8} ");
		sb.Append($"DE: {GetRegister(Constants.RegisterTargets.DE, Constants.DataSize.DWord):X8} ");
		sb.Append($"HL: {GetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord):X8} ");
		sb.Append($"IX: {GetRegister(Constants.RegisterTargets.IX, Constants.DataSize.DWord):X8} ");
		sb.Append($"IY: {GetRegister(Constants.RegisterTargets.IY, Constants.DataSize.DWord):X8} ");
		sb.Append($"SP: {GetRegister(Constants.RegisterTargets.SP, Constants.DataSize.DWord):X8} ");
		sb.Append($"PC: {GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord):X8} ");

		return sb.ToString();
	}

	public string GetAlternateDisplayString()
	{
		var sb = new StringBuilder();

		sb.Append($"AF': {GetRegister(Constants.RegisterTargets.AF_, Constants.DataSize.DWord):X8} ");
		sb.Append($"BC': {GetRegister(Constants.RegisterTargets.BC_, Constants.DataSize.DWord):X8} ");
		sb.Append($"DE': {GetRegister(Constants.RegisterTargets.DE_, Constants.DataSize.DWord):X8} ");
		sb.Append($"HL': {GetRegister(Constants.RegisterTargets.HL_, Constants.DataSize.DWord):X8} ");
		sb.Append($"IX': {GetRegister(Constants.RegisterTargets.IX_, Constants.DataSize.DWord):X8} ");
		sb.Append($"IY': {GetRegister(Constants.RegisterTargets.IY_, Constants.DataSize.DWord):X8} ");
		sb.Append($"SP': {GetRegister(Constants.RegisterTargets.SP_, Constants.DataSize.DWord):X8} ");

		return sb.ToString();
	}

	public string GetSystemDisplayString()
	{
		var sb = new StringBuilder();

		sb.Append($"I: {GetRegister(Constants.RegisterTargets.I, Constants.DataSize.DWord):X8} ");
		sb.Append($"R: {GetRegister(Constants.RegisterTargets.R, Constants.DataSize.Word):X4} ");

		return sb.ToString();
	}

	public string GetFlagsDisplayString()
	{
		var sb = new StringBuilder();

		sb.Append($"C: {GetFlag(Constants.FlagMasks.Carry)} ");
		sb.Append($"N: {GetFlag(Constants.FlagMasks.Subtract)} ");
		sb.Append($"PV: {GetFlag(Constants.FlagMasks.ParityOverflow)} ");
		sb.Append($"H: {GetFlag(Constants.FlagMasks.HalfCarry)} ");
		sb.Append($"Z: {GetFlag(Constants.FlagMasks.Zero)} ");
		sb.Append($"S: {GetFlag(Constants.FlagMasks.Sign)} ");
		sb.Append($"IE: {GetFlag(Constants.FlagMasks.EnableInterrupts)} ");
		sb.Append($"IFF2: {GetFlag(Constants.FlagMasks.EnableInterruptsSave)} )) ");

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
}
