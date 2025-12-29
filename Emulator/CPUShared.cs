namespace im8000emu.Emulator;

// Contains shared private members between CPU.cs, CPUDecode.cs, and CPUExecute.cs
internal partial class CPU
{
    private readonly MemoryBus _memoryBus;
    private readonly MemoryBus _ioBus;

    // TODO: Refactor to ReadMemory(uint address, Constants.OperandSize size)
    private uint ReadMemory(uint address, Constants.OperandSize size)
    {
        switch (size)
        {
            case Constants.OperandSize.Byte:
            {
                return _memoryBus.ReadByte(address);
            }

            case Constants.OperandSize.Word:
            {
                Span<byte> data = _memoryBus.ReadByteArray(address, 2);
                return BitConverter.ToUInt16(data);
            }

            case Constants.OperandSize.DWord:
            {
                Span<byte> data = _memoryBus.ReadByteArray(address, 4);
                return BitConverter.ToUInt32(data);
            }

            default:
            {
                throw new ArgumentException($"ReadMemory is not implemented for OperandSize {size}");
            }
        }
    }

    private void WriteMemory(uint address, Constants.OperandSize size, uint value)
    {
        switch (size)
        {
            case Constants.OperandSize.Byte:
            {
                _memoryBus.WriteByte(address, (byte)value);
                break;
            }

            case Constants.OperandSize.Word:
            {
                byte[] bytes = BitConverter.GetBytes(value);
                _memoryBus.WriteByteArray(address, bytes);
                break;
            }

            case Constants.OperandSize.DWord:
            {
                byte[] bytes = BitConverter.GetBytes(value);
                _memoryBus.WriteByteArray(address, bytes);
                break;
            }

            default:
            {
                throw new ArgumentException($"WriteMemory is not implemented for OperandSize {size}");
            }
        }
    }

    private (uint value, int cycles) GetOperandValue(Operand operand, Constants.OperandSize size)
    {
        if (operand.Target is null && operand.Immediate is null)
        {
            throw new ArgumentException("Either Target or Immediate must have a value");
        }

        uint value = 0;
        int cycles = 0;

        if (operand.Indirect)
        {
            uint address = GetEffectiveAddress(operand);
            value = ReadMemory(address, size);
            cycles = GetMemoryCycles(address, size);
        }
        else
        {
            if (operand.Target is not null)
            {
                value = Registers.GetRegister(operand.Target.Value, size);
            }
            else if (operand.Immediate is not null)
            {
                value = operand.Immediate.Value;
            }
        }

        return (value, cycles);
    }

    private int WritebackOperand(Operand operand, Constants.OperandSize size, uint value)
    {
        if (operand.Target is null && operand.Immediate is null)
        {
            throw new ArgumentException("Either Target or Immediate must have a value");
        }

        int cycles = 0;

        if (operand.Indirect)
        {
            uint address = GetEffectiveAddress(operand);
            WriteMemory(address, size, value);
            cycles = GetMemoryCycles(address, size);
        }
        else
        {
            if (operand.Target is null)
            {
                throw new ArgumentException("Cannot writeback to an immediate operand");
            }

            Registers.SetRegister(operand.Target.Value, size, value);
        }

        return cycles;
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

    private static int GetMemoryCycles(uint address, Constants.OperandSize size)
    {
        bool aligned = address % 2 == 0;

        return size switch
        {
            Constants.OperandSize.Byte => 3,
            Constants.OperandSize.Word => aligned ? 3 : 6,
            Constants.OperandSize.DWord => aligned ? 6 : 9,
            _ => throw new ArgumentException($"{size} is not a valid operand size")
        };
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
}
