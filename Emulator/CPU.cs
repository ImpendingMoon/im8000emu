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

        // If last instruction raised an exception, handle it
        // var decodedOperation = new DecodedOperation()
        // {
        //     BaseAddress = Registers.GetRegisterDWord(Constants.RegisterTargets.PC),
        //     DisplayString = "Exception Handler",
        //     Opcode = new byte[] { 0xFF } // Placeholder, read exception vector number from CPU state
        // };
        // ... Set execute callback to handle exception ...
        // _executeCallbackLength = 0;
        // return decodedOperation;

        // Else if waiting for interrupts, handle them
        // var decodedOperation = new DecodedOperation()
        // {
        //     BaseAddress = Registers.GetRegisterDWord(Constants.RegisterTargets.PC),
        //     DisplayString = "Maskable Interrupt", // Or Non-Maskable Interrupt
        //     Opcode = new byte[] { 0xFF } // Placeholder, read vector number from interrupt bus
        // };
        // ... Set execute callback to handle interrupt ...
        // _executeCallbackLength = 0;
        // return decodedOperation;

        // Else if HALT state, return HALT operation
        // var decodedOperation = new DecodedOperation()
        // {
        //     BaseAddress = Registers.GetRegisterDWord(Constants.RegisterTargets.PC),
        //     DisplayString = "Halted",
        //     Opcode = new byte[] { } // Not reading any opcode
        // };
        // _executeCallback = () => 4; // HALT takes 4 T-cycles per iteration
        // _executeCallbackLength = 0;
        // return decodedOperation;

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
            Opcode = [0x00]
        };

        _executeCallback = () =>
        {
            return 4;
        };
        _executeCallbackLength = decodedOperation.Opcode.Length;

        return decodedOperation;
    }

    /// <summary>
    /// Executes the last decoded operation.
    /// </summary>
    /// <returns>Number of T-cycles taken.</returns>
    public int Execute()
    {
        // Actually read memory at PC in case reads have side effects
        // Like if a program pointed PC at MMIO for some reason

        // Technically part of Fetch, but cleaner to have here so Decode has no side effects
        uint pc = Registers.GetRegisterDWord(Constants.RegisterTargets.PC);
        pc += (uint)_executeCallbackLength;
        Registers.SetRegisterDWord(Constants.RegisterTargets.PC, pc);

        return _executeCallback();
    }

    // Callback to use in Execute(), set by Decode().
    private Func<int> _executeCallback = () => 0;

    // Length of the last decoded operation's opcode.
    private int _executeCallbackLength = 0;
}
