namespace im8000emu.Emulator;

// Contains shared private members between CPU.cs, CPUDecode.cs, and CPUExecute.cs
internal partial class CPU
{
    private readonly MemoryBus _memoryBus;
    private readonly MemoryBus _ioBus;

    private MemoryResult ReadMemory(uint address, Constants.OperandSize size)
    {
        var result = new MemoryResult();

        bool aligned = address % 2 == 0;

        switch (size)
        {
            case Constants.OperandSize.Byte:
            {
                result.Value = _memoryBus.ReadByte(address);
                result.Cycles = 3;
                break;
            }

            case Constants.OperandSize.Word:
            {
                Span<byte> data = _memoryBus.ReadByteArray(address, 2);
                result.Value = BitConverter.ToUInt16(data);
                result.Cycles = aligned ? 3 : 6;
                break;
            }

            case Constants.OperandSize.DWord:
            {
                Span<byte> data = _memoryBus.ReadByteArray(address, 4);
                result.Value = BitConverter.ToUInt32(data);
                result.Cycles = aligned ? 6 : 9;
                break;
            }

            default:
            {
                throw new ArgumentException($"ReadMemory is not implemented for OperandSize {size}");
            }
        }

        return result;
    }

    private MemoryResult WriteMemory(uint address, Constants.OperandSize size, uint value)
    {
        var result = new MemoryResult()
        {
            Value = 0
        };

        bool aligned = address % 2 == 0;

        switch (size)
        {
            case Constants.OperandSize.Byte:
            {
                _memoryBus.WriteByte(address, (byte)value);
                result.Cycles = 3;
                break;
            }

            case Constants.OperandSize.Word:
            {
                byte[] bytes = BitConverter.GetBytes(value);
                _memoryBus.WriteByteArray(address, bytes);
                result.Cycles = aligned ? 3 : 6;
                break;
            }

            case Constants.OperandSize.DWord:
            {
                byte[] bytes = BitConverter.GetBytes(value);
                _memoryBus.WriteByteArray(address, bytes);
                result.Cycles = aligned ? 6 : 9;
                break;
            }

            default:
            {
                throw new ArgumentException($"WriteMemory is not implemented for OperandSize {size}");
            }
        }

        return result;
    }

    private MemoryResult GetOperandValue(Operand operand, Constants.OperandSize size)
    {
        if (operand.Target is null && operand.Immediate is null)
        {
            throw new ArgumentException("Either Target or Immediate must have a value");
        }

        var result = new MemoryResult();

        if (operand.Indirect)
        {
            uint address = GetEffectiveAddress(operand);
            result = ReadMemory(address, size);
        }
        else
        {
            if (operand.Target is not null)
            {
                result.Value = Registers.GetRegister(operand.Target.Value, size);
            }
            else if (operand.Immediate is not null)
            {
                result.Value = operand.Immediate.Value;
            }

            result.Cycles = 0;
        }

        return result;
    }

    private MemoryResult WritebackOperand(Operand operand, Constants.OperandSize size, uint value)
    {
        if (operand.Target is null && operand.Immediate is null)
        {
            throw new ArgumentException("Either Target or Immediate must have a value");
        }

        var result = new MemoryResult();

        if (operand.Indirect)
        {
            uint address = GetEffectiveAddress(operand);
            result = WriteMemory(address, size, value);
        }
        else
        {
            if (operand.Target is null)
            {
                throw new ArgumentException("Cannot writeback to an immediate operand");
            }

            Registers.SetRegister(operand.Target.Value, size, value);
            result.Cycles = 0;
        }

        return result;
    }

    private uint GetEffectiveAddress(Operand operand)
    {
        uint address = 0;

        if (operand.Target is null && operand.Immediate is null)
        {
            throw new ArgumentException("Either Target or Immediate must have a value");
        }

        if (operand.Target is not null)
        {
            address = Registers.GetRegister(operand.Target.Value, Constants.OperandSize.DWord);
        }
        else if (operand.Immediate is not null)
        {
            address = operand.Immediate.Value;
        }

        if (operand.Displacement is not null)
        {
            address = (uint)((int)address + operand.Displacement.Value);
        }

        return address;
    }

    private void ExchangeWithAlternate(Constants.RegisterTargets register, Constants.OperandSize size)
    {
        // In hardware, this would just be a mux bit flip
        // In software, easier to actually exchange the values
        Constants.RegisterTargets alternate = Constants.RegisterToAlternate[register];

        ushort primaryValue = (ushort)Registers.GetRegister(register, Constants.OperandSize.Word);
        ushort alternateValue = (ushort)Registers.GetRegister(alternate, Constants.OperandSize.Word);

        Registers.SetRegister(alternate, Constants.OperandSize.Word, primaryValue);
        Registers.SetRegister(register, Constants.OperandSize.Word, alternateValue);
    }

    private uint FetchImmediate(DecodedOperation decodedOperation, Constants.OperandSize size)
    {
        MemoryResult immediateFetch = ReadMemory((uint)(decodedOperation.BaseAddress + decodedOperation.Opcode.Count), size);
        decodedOperation.FetchCycles += immediateFetch.Cycles;
        AddValueToOpcode(decodedOperation.Opcode, size, immediateFetch.Value);
        return immediateFetch.Value;
    }

    private static void AddValueToOpcode(List<byte> opcode, Constants.OperandSize size, uint value)
    {
        byte[] immediateBytes = BitConverter.GetBytes(value);

        switch (size)
        {
            case Constants.OperandSize.Byte:
            {
                opcode.Add(immediateBytes[0]);
                break;
            }

            case Constants.OperandSize.Word:
            {
                opcode.AddRange(immediateBytes[0..2]);
                break;
            }

            case Constants.OperandSize.DWord:
            {
                opcode.AddRange(immediateBytes);
                break;
            }

            default: throw new ArgumentException($"{size} is not a valid operand size");
        }
    }

    private static string GetOperationString(Constants.Operation operation, Constants.OperandSize size)
    {
        return size switch
        {
            Constants.OperandSize.Byte => $"{operation}.B",
            Constants.OperandSize.Word => $"{operation}.W",
            Constants.OperandSize.DWord => $"{operation}.D",
            _ => throw new ArgumentException($"{size} is not a valid operand size"),
        };
    }

    private bool IsConditionTrue(Constants.Condition condition)
    {
        return condition switch
        {
            Constants.Condition.NZ => !Registers.GetFlag(Constants.FlagMasks.Zero),
            Constants.Condition.Z => Registers.GetFlag(Constants.FlagMasks.Zero),
            Constants.Condition.NC => !Registers.GetFlag(Constants.FlagMasks.Carry),
            Constants.Condition.C => Registers.GetFlag(Constants.FlagMasks.Carry),
            Constants.Condition.PO => !Registers.GetFlag(Constants.FlagMasks.ParityOverflow),
            Constants.Condition.PE => Registers.GetFlag(Constants.FlagMasks.ParityOverflow),
            Constants.Condition.P => !Registers.GetFlag(Constants.FlagMasks.Sign),
            Constants.Condition.N => Registers.GetFlag(Constants.FlagMasks.Sign),
            _ => throw new ArgumentException($"IsConditionTrue is not implemented for condition {condition}")
        };
    }

    private void UpdateALUFlags(ALUFlagState state)
    {
        Registers.SetFlag(Constants.FlagMasks.Carry, state.Carry);
        Registers.SetFlag(Constants.FlagMasks.Negative, state.Negative);
        Registers.SetFlag(Constants.FlagMasks.ParityOverflow, state.ParityOverflow);
        Registers.SetFlag(Constants.FlagMasks.HalfCarry, state.HalfCarry);
        Registers.SetFlag(Constants.FlagMasks.Zero, state.Zero);
        Registers.SetFlag(Constants.FlagMasks.Sign, state.Sign);
    }

    private struct MemoryResult
    {
        public uint Value;
        public int Cycles;
    }

    private struct ALUFlagState
    {
        public bool Carry;
        public bool Negative;
        public bool ParityOverflow;
        public bool HalfCarry;
        public bool Zero;
        public bool Sign;
    }
}
