namespace im8000emu.Emulator;

// Contains shared private members between CPU.cs, CPUDecode.cs, and CPUExecute.cs
internal partial class CPU
{
    private readonly MemoryBus _memoryBus;
    private readonly MemoryBus _ioBus;

    // TODO: Refactor to ReadMemory(uint address, Constants.OperandSize size)
    private byte ReadMemoryByte(uint address)
    {
        return _memoryBus.ReadByte(address);
    }

    private ushort ReadMemoryWord(uint address)
    {
        Span<byte> data = _memoryBus.ReadByteArray(address, 2);
        return BitConverter.ToUInt16(data);
    }

    private uint ReadMemoryDWord(uint address)
    {
        Span<byte> data = _memoryBus.ReadByteArray(address, 4);
        return BitConverter.ToUInt32(data);
    }

    private void WriteMemoryByte(uint address, byte value)
    {
        _memoryBus.WriteByte(address, value);
    }

    private void WriteMemoryWord(uint address, ushort value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        _memoryBus.WriteByteArray(address, bytes);
    }

    private void WriteMemoryDWord(uint address, uint value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        _memoryBus.WriteByteArray(address, bytes);
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

            value = size switch
            {
                Constants.OperandSize.Byte => ReadMemoryByte(address),
                Constants.OperandSize.Word => ReadMemoryWord(address),
                Constants.OperandSize.DWord => ReadMemoryDWord(address),
                _ => throw new ArgumentException($"{size} is not a valid operand size")
            };

            cycles = GetMemoryCycles(address, size);
        }
        else
        {
            if (operand.Target is not null)
            {
                value = size switch
                {
                    Constants.OperandSize.Byte => Registers.GetRegisterByte(operand.Target.Value, false),
                    Constants.OperandSize.Word => Registers.GetRegisterWord(operand.Target.Value),
                    Constants.OperandSize.DWord => Registers.GetRegisterDWord(operand.Target.Value),
                    _ => throw new ArgumentException($"{size} is not a valid operand size")
                };
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

            switch (size)
            {
                case Constants.OperandSize.Byte:
                    WriteMemoryByte(address, (byte)value);
                    break;
                case Constants.OperandSize.Word:
                    WriteMemoryWord(address, (ushort)value);
                    break;
                case Constants.OperandSize.DWord:
                    WriteMemoryDWord(address, value);
                    break;
                default:
                    throw new ArgumentException($"{size} is not a valid operand size");
            }

            cycles = GetMemoryCycles(address, size);
        }
        else
        {
            if (operand.Target is null)
            {
                throw new ArgumentException("Cannot writeback to an immediate operand");
            }

            switch (size)
            {
                case Constants.OperandSize.Byte:
                    Registers.SetRegisterByte(operand.Target.Value, (byte)value, false);
                    break;
                case Constants.OperandSize.Word:
                    Registers.SetRegisterWord(operand.Target.Value, (ushort)value);
                    break;
                case Constants.OperandSize.DWord:
                    Registers.SetRegisterDWord(operand.Target.Value, value);
                    break;
            }
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
            address = Registers.GetRegisterDWord(operand.Target.Value);
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

        switch (size)
        {
            case Constants.OperandSize.Word:
            {
                ushort primaryValue = Registers.GetRegisterWord(register);
                ushort alternateValue = Registers.GetRegisterWord(alternate);

                Registers.SetRegisterWord(alternate, primaryValue);
                Registers.SetRegisterWord(register, alternateValue);

                break;
            }

            case Constants.OperandSize.DWord:
            {
                uint primaryValue = Registers.GetRegisterDWord(register);
                uint alternateValue = Registers.GetRegisterDWord(alternate);

                Registers.SetRegisterDWord(alternate, primaryValue);
                Registers.SetRegisterDWord(register, alternateValue);

                break;
            }
        }
    }

    private uint ReadImmediateValue(uint address, Constants.OperandSize size)
    {
        return size switch
        {
            Constants.OperandSize.Byte => ReadMemoryByte(address),
            Constants.OperandSize.Word => ReadMemoryWord(address),
            Constants.OperandSize.DWord => ReadMemoryDWord(address),
            _ => throw new ArgumentException($"{size} is not a valid operand size"),
        };
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
