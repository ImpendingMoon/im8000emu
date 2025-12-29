namespace im8000emu.Emulator;

// This class is split into CPU.cs, CPUDecode.cs, CPUExecute.cs, and CPUShared.cs
// It is a beast of a class with several thousand LOC, which is expected for an emulator core
//
// I need to refactor some data flow stuff, and we *need* to get better exceptions and disassembly for debugging.
// Read/WriteMemory should own cycle calculation, and use a named struct instead of tuple.
// Disassembly is tolerable, but doesn't match 1:1 with assembler, and has nonsense like JR_s8.B -12
internal partial class CPU
{
    private Dictionary<Constants.Operation, Func<DecodedOperation, int>> _operationExecutors;

    public CPU(MemoryBus memoryBus, MemoryBus ioBus)
    {
        _memoryBus = memoryBus;
        _ioBus = ioBus;

        // Methods defined in CPUExecute.cs, same order as Constants.Operation enum
        _operationExecutors = new Dictionary<Constants.Operation, Func<DecodedOperation, int>>()
        {
            { Constants.Operation.None, Execute_None },
            { Constants.Operation.Interrupt, Execute_Interrupt },
            { Constants.Operation.NonMaskableInterrupt, Execute_NonMaskableInterrupt },
            { Constants.Operation.HaltState, Execute_HaltState },
            { Constants.Operation.LD, Execute_LD },
            { Constants.Operation.EX, Execute_EX },
            { Constants.Operation.EX_Alt, Execute_EX_Alt },
            { Constants.Operation.EXX, Execute_EXX },
            { Constants.Operation.EXI, Execute_EXI },
            { Constants.Operation.EXH, Execute_EXH },
            { Constants.Operation.PUSH, Execute_PUSH },
            { Constants.Operation.POP, Execute_POP },
            { Constants.Operation.IN, Execute_IN },
            { Constants.Operation.OUT, Execute_OUT },
            { Constants.Operation.LDI, Execute_LDI },
            { Constants.Operation.LDIR, Execute_LDIR },
            { Constants.Operation.LDD, Execute_LDD  },
            { Constants.Operation.LDDR, Execute_LDDR },
            { Constants.Operation.CPI, Execute_CPI },
            { Constants.Operation.CPIR, Execute_CPIR },
            { Constants.Operation.CPD, Execute_CPD },
            { Constants.Operation.CPDR, Execute_CPDR },
            { Constants.Operation.TSI, Execute_TSI  },
            { Constants.Operation.TSIR, Execute_TSIR },
            { Constants.Operation.TSD, Execute_TSD },
            { Constants.Operation.TSDR, Execute_TSDR },
            { Constants.Operation.INI, Execute_INI },
            { Constants.Operation.INIR, Execute_INIR },
            { Constants.Operation.IND, Execute_IND },
            { Constants.Operation.INDR, Execute_INDR },
            { Constants.Operation.OUTI, Execute_OUTI },
            { Constants.Operation.OTIR, Execute_OTIR },
            { Constants.Operation.OUTD, Execute_OUTD },
            { Constants.Operation.OTDR, Execute_OTDR },
            { Constants.Operation.ADD, Execute_ADD },
            { Constants.Operation.ADC, Execute_ADC },
            { Constants.Operation.SUB, Execute_SUB },
            { Constants.Operation.SBC, Execute_SBC },
            { Constants.Operation.CP, Execute_CP },
            { Constants.Operation.INC, Execute_INC },
            { Constants.Operation.DEC, Execute_DEC },
            { Constants.Operation.DAA, Execute_DAA },
            { Constants.Operation.NEG, Execute_NEG },
            { Constants.Operation.EXT, Execute_EXT },
            { Constants.Operation.MLT, Execute_MLT },
            { Constants.Operation.DIV, Execute_DIV },
            { Constants.Operation.SDIV, Execute_SDIV },
            { Constants.Operation.AND, Execute_AND },
            { Constants.Operation.OR, Execute_OR },
            { Constants.Operation.XOR, Execute_XOR },
            { Constants.Operation.TST, Execute_TST },
            { Constants.Operation.CPL, Execute_CPL },
            { Constants.Operation.BIT, Execute_BIT },
            { Constants.Operation.SET, Execute_SET },
            { Constants.Operation.RES, Execute_RES },
            { Constants.Operation.RLC, Execute_RLC },
            { Constants.Operation.RRC, Execute_RRC },
            { Constants.Operation.RL, Execute_RL },
            { Constants.Operation.RR, Execute_RR },
            { Constants.Operation.SLA, Execute_SLA },
            { Constants.Operation.SRA, Execute_SRA },
            { Constants.Operation.SRL, Execute_SRL },
            { Constants.Operation.RLD, Execute_RLD  },
            { Constants.Operation.RRD, Execute_RRD },
            { Constants.Operation.NOP, Execute_NOP },
            { Constants.Operation.JP, Execute_JP },
            { Constants.Operation.JR_s8, Execute_JR_s8 },
            { Constants.Operation.JR, Execute_JR },
            { Constants.Operation.CALL, Execute_CALL },
            { Constants.Operation.CALLR_s8, Execute_CALLR_s8 },
            { Constants.Operation.CALLR, Execute_CALLR },
            { Constants.Operation.RET, Execute_RET },
            { Constants.Operation.RETI, Execute_RETI },
            { Constants.Operation.RETN, Execute_RETN },
            { Constants.Operation.DJNZ, Execute_DJNZ },
            { Constants.Operation.JANZ, Execute_JANZ },
            { Constants.Operation.RST, Execute_RST },
            { Constants.Operation.CCF, Execute_CCF },
            { Constants.Operation.SCF, Execute_SCF },
            { Constants.Operation.EI, Execute_EI },
            { Constants.Operation.DI, Execute_DI },
            { Constants.Operation.IM1, Execute_IM1  },
            { Constants.Operation.IM2, Execute_IM2 },
            { Constants.Operation.HALT, Execute_HALT },
            { Constants.Operation.LD_I_NN, Execute_LD_I_NN },
            { Constants.Operation.LD_R_A, Execute_LD_R_A },
            { Constants.Operation.LD_A_R, Execute_LD_A_R },
        };
    }

    public Registers Registers { get; } = new Registers();

    public void Reset()
    {
        Registers.ClearRegisters();
        // Read reset vector
        uint resetVector = ReadMemoryDWord(0x00000000);
        Registers.SetRegisterDWord(Constants.RegisterTargets.PC, resetVector);
    }

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

        int cycles = _operationExecutors[instruction.Operation](instruction);
        return cycles;
    }
}
