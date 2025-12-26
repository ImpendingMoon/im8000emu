using System.Diagnostics;

namespace im8000emu.Emulator;

internal class CPU
{
    public CPU(MemoryBus memoryBus, MemoryBus ioBus)
    {
        _memoryBus = memoryBus;
        _ioBus = ioBus;
    }

    public Registers Registers { get; } = new Registers();

    /// <summary>
    /// Fetches and decodes the next operation, including interrupt servicing.
    /// </summary>
    public DecodedOperation Decode()
    {
        // If waiting for interrupts, handle them

        // Else if HALT state, return HALT operation

        // Else decode the operation at the current PC
        uint pc = Registers.GetRegisterDWord(Constants.RegisterTargets.PC);
        return Decode(pc);
    }

    /// <summary>
    /// Fetches and decodes the operation at the given address. Does not include interrupt servicing.
    /// </summary>
    /// <param name="address">Base address of the opcode</param>
    public DecodedOperation Decode(uint address)
    {
        // Placeholder
        var decodedOperation = new DecodedOperation
        {
            BaseAddress = address,
            Opcode = [_memoryBus.ReadByte(address)]
        };

        var group = decodedOperation.Opcode[0] & 0b00000011;

        // TODO: Implement methods to decode each instruction group. Probably in separate files (because decoding+execution will be thousands of lines long).
        switch (group)
        {
            case 0b00:
            {
                // Register-Register Instructions
                break;
            }

            case 0b01:
            {
                // Register-Memory Instructions
                break;
            }

            case 0b10:
            {
                // Sub-Grouped Instructions
                var subgroup = (decodedOperation.Opcode[0] >> 2) & 0b0000011;
                switch (subgroup)
                {
                    case 0b00:
                    {
                        // Unary Register Instructions
                        break;
                    }
                    case 0b01:
                    {
                        // Unary Memory Instructions
                        break;
                    }
                    case 0b10:
                    {
                        // Branch Instructions
                        break;
                    }
                    case 0b11:
                    {
                        // Nullary Instructions
                        break;
                    }
                    default:
                    {
                        Debug.Assert(false, "Unreachable code reached in opcode decoding.");
                        break;
                    }
                }

                break;
            }

            case 0b11:
            {
                // Variable Length Instructions
                var subgroup = (decodedOperation.Opcode[0] >> 2) & 0b0000011;
                switch (subgroup)
                {
                    case 0b11:
                    {
                        // Single byte Instructions
                        break;
                    }
                }
                break;
            }

            default:
            {
                Debug.Assert(false, "Unreachable code reached in opcode decoding.");
                break;
            }
        }

        return decodedOperation;
    }

    /// <summary>
    /// Executes the decoded operation.
    /// </summary>
    /// <returns>Number of T-cycles taken.</returns>
    public int Execute(DecodedOperation instruction)
    {
        // Advance PC
        uint pc = Registers.GetRegisterDWord(Constants.RegisterTargets.PC);
        pc += (uint)instruction.Opcode.Length;
        Registers.SetRegisterDWord(Constants.RegisterTargets.PC, pc);

        // Execute instruction

        // Return number of cycles taken
        return 4;
    }

    private readonly MemoryBus _memoryBus;
    private readonly MemoryBus _ioBus;
}
