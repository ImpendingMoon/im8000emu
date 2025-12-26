namespace im8000emu.Emulator;

internal static class Constants
{
    public enum RegisterTargets
    {
        // 16-bit registers
        A, F, B, C, D, E, H, L, IXH, IXL, IYH, IYL, SPH, SPL,
        // 32-bit register views
        AF, BC, DE, HL, IX, IY, SP,
        // 16-bit alternate registers
        A_, F_, B_, C_, D_, E_, H_, L_, IXL_, IXH_, IYL_, IYH_, SPL_, SPH_,
        // 32-bit alternate register views
        AF_, BC_, DE_, HL_, IX_, IY_, SP_,
        // State registers
        PC, I, R,
        IFF2,
    }

    public enum OperandSize
    {
        Byte,
        Word,
        DWord,
    }

    public enum FlagMasks : ushort
    {
        EnableInterrupts = 0b0000_0001_0000_0000,
        Sign = 0b1000_0000,
        Zero = 0b0100_0000,
        Unused5 = 0b0010_0000,
        HalfCarry = 0b0001_0000,
        Unused3 = 0b0000_1000,
        ParityOverflow = 0b0000_0100,
        Negative = 0b0000_0010,
        Carry = 0b0000_0001,
    }

    public enum Condition
    {
        NZ,
        Z,
        NC,
        C,
        PO,
        PE,
        P,
        M,
        Unconditional,
    }

    public enum BranchMode
    {
        Relative8,
        Relative16,
        Direct,
        Return,
    }

    public enum Operation
    {
        None,   // Unknown/Illegal operation
        Interrupt,
        NonMaskableInterrupt,
        Halt,

        // Load and store
        LD,     // Load
        EX,     // Exchange
        EX_Alt, // Exchange with Alternate
        EXX,    // Exchange Primary
        EXI,    // Exchange Index
        EXH,    // Exchange Halves
        PUSH,   // Push to stack
        POP,    // Pop from stack
        IN,     // In from I/O
        OUT,    // Out to I/O

        // Block operations
        LDI,    // Load and Increment
        LDIR,   // Load, Increment, and Repeat
        LDD,    // Load and Decrement
        LDDR,   // Load, Decrement, and Repeat
        CPI,    // Compare and Increment
        CPIR,   // Compare, Increment, and Repeat
        CPD,    // Compare and Decrement
        CPDR,   // Compare, Decrement, and Repeat
        TSI,    // Test and Increment
        TSIR,   // Test, Increment, and Repeat
        TSD,    // Test and Decrement
        TSDR,   // Test, Decrement, and Repeat
        INI,    // In and Increment
        INIR,   // In, Increment, and Repeat
        IND,    // In and Decrement
        INDR,   // In, Decrement, and Repeat
        OUTI,   // Out and Increment
        OTIR,   // Out, Increment, and Repeat
        OUTD,   // Out and Decrement
        OTDR,   // Out, Decrement, and Repeat

        // Arithmetic
        ADD,    // Add
        ADC,    // Add with Carry
        SUB,    // Subtract
        SBC,    // Subtract with Carry
        CP,     // Compare
        INC,    // Increment
        DEC,    // Decrement
        DAA,    // Decimal Adjust A
        NEG,    // Negate
        EXT,    // Sign Extend
        MLT,    // Multiply
        DIV,    // Divide
        SDIV,   // Signed Divide

        // Logical
        AND,    // Bitwise AND
        OR,     // Bitwise OR
        XOR,    // Bitwise XOR
        TST,    // Test
        CPL,    // Complement
        BIT,    // Test Bit
        SET,    // Set Bit
        RES,    // Reset Bit
        RLC,    // Rotate Left with Carry
        RRC,    // Rotate Right with Carry
        RL,     // Rotate Left
        RR,     // Rotate Right
        SLA,    // Shift Left Arithmetic
        SRA,    // Shift Right Arithmetic
        SRL,    // Shift Right Logical
        RLD,    // Rotate Left Decimal
        RRD,    // Rotate Right Decimal

        // Flow control
        NOP,    // No Operation
        JP,     // Jump/Jump Relative
        DJNZ,   // Decrement, Jump if Not Zero
        JANZ,   // Jump if A is Not Zero
        CALL,   // Call/Call Relative
        RET,    // Return
        RETI,   // Return from Interrupt
        RETN,   // Return from Non-Maskable Interrupt
        RST,    // Software Interrupt
        SCF,    // Set Carry Flag
        CCF,    // Complement Carry Flag

        // System
        EI,     // Enable Interrupts
        DI,     // Disable Interrupts
        IM1,    // Interrupt Mode 1
        IM2,    // Interrupt Mode 2
        HALT,   // Halt
        LD_I_NN,// Load Immediate into I
        LD_R_A, // Load A into R
        LD_A_R, // Load R into A
    }
}
