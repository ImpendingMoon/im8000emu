namespace im8000emu.Emulator;

internal class CPU
{
    public CPU()
    {
    }

    public Registers Registers { get; } = new Registers();

    /// <summary>
    /// Decodes the next operation, including interrupt servicing.
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
    /// Decodes the operation at the given address, does not include interrupt servicing.
    /// </summary>
    /// <param name="address">Base address of the opcode</param>
    public DecodedOperation Decode(uint address)
    {
        // Placeholder
        var decodedOperation = new DecodedOperation
        {
            BaseAddress = address,
            DisplayString = "NOP",
            Opcode = [0b00001111]
        };

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
        pc += 2;
        Registers.SetRegisterDWord(Constants.RegisterTargets.PC, pc);

        // Execute instruction

        // Return number of cycles taken
        return 4;
    }
}
