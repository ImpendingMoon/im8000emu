namespace im8000emu.Emulator;

internal partial class CPU
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
        // Overload defined in CPUDecode.cs
        return Decode(pc);
    }

    /// <summary>
    /// Executes the decoded operation.
    /// </summary>
    /// <returns>Number of T-cycles taken.</returns>
    public int Execute(DecodedOperation instruction)
    {
        // Advance PC
        uint pc = Registers.GetRegisterDWord(Constants.RegisterTargets.PC);
        pc += (uint)instruction.Opcode.Count;
        Registers.SetRegisterDWord(Constants.RegisterTargets.PC, pc);

        // Execute instruction

        // Return number of cycles taken
        return 4;
    }

    private byte ReadMemoryByte(uint address)
    {
        return _memoryBus.ReadByte(address);
    }

    private ushort ReadMemoryWord(uint address)
    {
        var data = _memoryBus.ReadByteArray(address, 2);
        return BitConverter.ToUInt16(data);
    }

    private uint ReadMemoryDWord(uint address)
    {
        var data = _memoryBus.ReadByteArray(address, 4);
        return BitConverter.ToUInt32(data);
    }

    private readonly MemoryBus _memoryBus;
    private readonly MemoryBus _ioBus;
}
