namespace im8000emu.Emulator;

// Implements methods to execute each operation
internal partial class CPU
{
    private int Execute_None(DecodedOperation operation)
    {
        // Here for completeness. None should be caught in decoder.
        return operation.FetchCycles;
    }

    private int Execute_Interrupt(DecodedOperation operation)
    {
        // Interrupt handling logic would go here
        return operation.FetchCycles + 7; // Example cycle count for interrupt handling
    }

    private int Execute_NonMaskableInterrupt(DecodedOperation operation)
    {
        // NMI handling logic would go here
        return operation.FetchCycles + 11; // Example cycle count for NMI handling
    }

    // Spin
    private int Execute_HaltState(DecodedOperation operation)
    {
        return 4;
    }

    // Load
    private int Execute_LD(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("LD requires two operands");
        }

        int cycles = operation.FetchCycles + 1;

        (uint value, int readCycles) = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += readCycles;

        cycles += WritebackOperand(operation.Operand1, operation.OperandSize, value);

        return cycles;
    }

    // Exchange
    private int Execute_EX(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("EX requires two operands");
        }

        int cycles = operation.FetchCycles + 1;

        (uint value1, int readCycles1) = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += readCycles1;
        (uint value2, int readCycles2) = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += readCycles2;

        cycles += WritebackOperand(operation.Operand1, operation.OperandSize, value2);
        cycles += WritebackOperand(operation.Operand2, operation.OperandSize, value1);

        return cycles;
    }

    // Exchange with Alternate
    private int Execute_EX_Alt(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("EX_Alt requires one operand");
        }

        if (operation.Operand1.Target is null || operation.Operand1.Indirect)
        {
            throw new ArgumentException("EX_Alt can only operate on register targets");
        }

        if (operation.OperandSize == Constants.OperandSize.Byte)
        {
            throw new ArgumentException("EX_Alt cannot be used with byte operands");
        }

        ExchangeWithAlternate(operation.Operand1.Target.Value, operation.OperandSize);

        return operation.FetchCycles + 1;
    }

    // Exchange Primary with Alternate
    private int Execute_EXX(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("EXX requires no operands");
        }

        ExchangeWithAlternate(Constants.RegisterTargets.BC, Constants.OperandSize.DWord);
        ExchangeWithAlternate(Constants.RegisterTargets.DE, Constants.OperandSize.DWord);
        ExchangeWithAlternate(Constants.RegisterTargets.HL, Constants.OperandSize.DWord);

        return operation.FetchCycles + 1;
    }

    // Exchange Index with Alternate
    private int Execute_EXI(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("EXI requires no operands");
        }

        ExchangeWithAlternate(Constants.RegisterTargets.IX, Constants.OperandSize.DWord);
        ExchangeWithAlternate(Constants.RegisterTargets.IY, Constants.OperandSize.DWord);
        ExchangeWithAlternate(Constants.RegisterTargets.SP, Constants.OperandSize.DWord);

        return operation.FetchCycles + 1;
    }

    // Exchange Halves
    private int Execute_EXH(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("EXH requires one operand");
        }

        int cycles = operation.FetchCycles + 1;

        (uint value, int readCycles) = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += readCycles;

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte temp = (byte)(value >> 4);
                temp |= (byte)(value << 4);
                value = temp;
                break;
            }
            case Constants.OperandSize.Word:
            {
                ushort temp = (ushort)(value >> 8);
                temp |= (ushort)(value << 8);
                value = temp;
                break;
            }
            case Constants.OperandSize.DWord:
            {
                uint temp = value >> 16;
                temp |= value << 16;
                value = temp;
                break;
            }
        }

        cycles += WritebackOperand(operation.Operand1, operation.OperandSize, value);

        return cycles;
    }

    // Push to stack
    private int Execute_PUSH(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("PUSH requires one operand");
        }

        int cycles = operation.FetchCycles + 1;

        (uint value, int readCycles) = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += readCycles;

        // PUSH sign extends into dwords
        if (operation.OperandSize == Constants.OperandSize.Byte)
        {
            value = Helpers.BitHelper.SignExtend(value, 8);
        }
        else if (operation.OperandSize == Constants.OperandSize.Word)
        {
            value = Helpers.BitHelper.SignExtend(value, 16);
        }

        uint stackPointer = Registers.GetRegisterDWord(Constants.RegisterTargets.SP);

        // Pre-decrement for PUSH
        stackPointer -= 2;

        // Write value to memory
        WriteMemoryDWord(stackPointer, value);

        // Writeback SP
        Registers.SetRegisterDWord(Constants.RegisterTargets.SP, stackPointer);

        // Calculate cycles
        bool aligned = stackPointer % 2 == 0;
        cycles += aligned ? 6 : 9;

        return cycles;
    }

    // Pop from stack
    private int Execute_POP(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("POP requires one operand");
        }

        int cycles = operation.FetchCycles + 1;

        uint stackPointer = Registers.GetRegisterDWord(Constants.RegisterTargets.SP);

        // Read value from memory
        uint value = ReadMemoryDWord(stackPointer);

        // Write to destination
        cycles += WritebackOperand(operation.Operand1, operation.OperandSize, value);

        // Post-increment for POP
        stackPointer += 2;

        // Writeback SP
        Registers.SetRegisterDWord(Constants.RegisterTargets.SP, stackPointer);

        // Calculate cycles
        bool aligned = stackPointer % 2 == 0;
        cycles += aligned ? 6 : 9;

        return cycles;
    }

    private int Execute_IN(DecodedOperation operation)
    {
        // Input from I/O logic would go here
        return operation.FetchCycles + 1; // Example cycle count for IN operation
    }

    private int Execute_OUT(DecodedOperation operation)
    {
        // Output to I/O logic would go here
        return operation.FetchCycles + 1; // Example cycle count for OUT operation
    }

    private int Execute_LDI(DecodedOperation operation)
    {
        // Load and Increment logic would go here
        return operation.FetchCycles + 1; // Example cycle count for LDI operation
    }

    private int Execute_LDIR(DecodedOperation operation)
    {
        // Load, Increment, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for LDIR operation
    }

    private int Execute_LDD(DecodedOperation operation)
    {
        // Load and Decrement logic would go here
        return operation.FetchCycles + 1; // Example cycle count for LDD operation
    }

    private int Execute_LDDR(DecodedOperation operation)
    {
        // Load, Decrement, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for LDDR operation
    }

    private int Execute_CPI(DecodedOperation operation)
    {
        // Compare and Increment logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CPI operation
    }

    private int Execute_CPIR(DecodedOperation operation)
    {
        // Compare, Increment, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CPIR operation
    }

    private int Execute_CPD(DecodedOperation operation)
    {
        // Compare and Decrement logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CPD operation
    }

    private int Execute_CPDR(DecodedOperation operation)
    {
        // Compare, Decrement, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CPDR operation
    }

    private int Execute_TSI(DecodedOperation operation)
    {
        // Test and Increment logic would go here
        return operation.FetchCycles + 1; // Example cycle count for TSI operation
    }

    private int Execute_TSIR(DecodedOperation operation)
    {
        // Test, Increment, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for TSIR operation
    }

    private int Execute_TSD(DecodedOperation operation)
    {
        // Test and Decrement logic would go here
        return operation.FetchCycles + 1; // Example cycle count for TSD operation
    }

    private int Execute_TSDR(DecodedOperation operation)
    {
        // Test, Decrement, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for TSDR operation
    }

    private int Execute_INI(DecodedOperation operation)
    {
        // Input and Increment logic would go here
        return operation.FetchCycles + 1; // Example cycle count for INI operation
    }

    private int Execute_INIR(DecodedOperation operation)
    {
        // Input, Increment, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for INIR operation
    }

    private int Execute_IND(DecodedOperation operation)
    {
        // Input and Decrement logic would go here
        return operation.FetchCycles + 1; // Example cycle count for IND operation
    }

    private int Execute_INDR(DecodedOperation operation)
    {
        // Input, Decrement, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for INDR operation
    }

    private int Execute_OUTI(DecodedOperation operation)
    {
        // Output and Increment logic would go here
        return operation.FetchCycles + 1; // Example cycle count for OUTI operation
    }

    private int Execute_OTIR(DecodedOperation operation)
    {
        // Output, Increment, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for OUTIR operation
    }

    private int Execute_OUTD(DecodedOperation operation)
    {
        // Output and Decrement logic would go here
        return operation.FetchCycles + 1; // Example cycle count for OUTD operation
    }

    private int Execute_OTDR(DecodedOperation operation)
    {
        // Output, Decrement, and Repeat logic would go here
        return operation.FetchCycles + 1; // Example cycle count for OTDR operation
    }

    private int Execute_ADD(DecodedOperation operation)
    {
        // Add operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for ADD operation
    }

    private int Execute_ADC(DecodedOperation operation)
    {
        // Add with Carry operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for ADC operation
    }

    private int Execute_SUB(DecodedOperation operation)
    {
        // Subtract operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for SUB operation
    }

    private int Execute_SBC(DecodedOperation operation)
    {
        // Subtract with Carry operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for SBC operation
    }

    private int Execute_CP(DecodedOperation operation)
    {
        // Compare operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CP operation
    }

    private int Execute_INC(DecodedOperation operation)
    {
        // Increment operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for INC operation
    }

    private int Execute_DEC(DecodedOperation operation)
    {
        // Decrement operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for DEC operation
    }

    private int Execute_DAA(DecodedOperation operation)
    {
        // Decimal Adjust for Addition logic would go here
        return operation.FetchCycles + 1; // Example cycle count for DAA operation
    }

    private int Execute_NEG(DecodedOperation operation)
    {
        // Negate operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for NEG operation
    }

    private int Execute_EXT(DecodedOperation operation)
    {
        // Sign Extend operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for EXT operation
    }

    private int Execute_MLT(DecodedOperation operation)
    {
        // Multiply operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for MLT operation
    }

    private int Execute_DIV(DecodedOperation operation)
    {
        // Divide operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for DIV operation
    }

    private int Execute_SDIV(DecodedOperation operation)
    {
        // Signed Divide operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for SDIV operation
    }

    private int Execute_AND(DecodedOperation operation)
    {
        // AND operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for AND operation
    }

    private int Execute_OR(DecodedOperation operation)
    {
        // OR operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for OR operation
    }

    private int Execute_XOR(DecodedOperation operation)
    {
        // XOR operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for XOR operation
    }

    private int Execute_TST(DecodedOperation operation)
    {
        // Test operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for TST operation
    }

    private int Execute_CPL(DecodedOperation operation)
    {
        // Complement operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CPL operation
    }

    private int Execute_BIT(DecodedOperation operation)
    {
        // Bit test operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for BIT operation
    }

    private int Execute_SET(DecodedOperation operation)
    {
        // Set bit operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for SET operation
    }

    private int Execute_RES(DecodedOperation operation)
    {
        // Reset bit operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RES operation
    }

    private int Execute_RLC(DecodedOperation operation)
    {
        // Rotate Left with Carry operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RLC operation
    }

    private int Execute_RRC(DecodedOperation operation)
    {
        // Rotate Right with Carry operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RRC operation
    }

    private int Execute_RL(DecodedOperation operation)
    {
        // Rotate Left operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RL operation
    }

    private int Execute_RR(DecodedOperation operation)
    {
        // Rotate Right operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RR operation
    }

    private int Execute_SLA(DecodedOperation operation)
    {
        // Shift Left Arithmetic operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for SLA operation
    }

    private int Execute_SRA(DecodedOperation operation)
    {
        // Shift Right Arithmetic operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for SRA operation
    }

    private int Execute_SRL(DecodedOperation operation)
    {
        // Shift Right Logical operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for SRL operation
    }

    private int Execute_RLD(DecodedOperation operation)
    {
        // Rotate Left Decimal operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RLD operation
    }

    private int Execute_RRD(DecodedOperation operation)
    {
        // Rotate Right Decimal operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RRD operation
    }

    // No operation
    private int Execute_NOP(DecodedOperation operation)
    {
        return operation.FetchCycles + 1;
    }

    private int Execute_JP(DecodedOperation operation)
    {
        // Jump operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for JP operation
    }

    private int Execute_JR_s8(DecodedOperation operation)
    {
        // Jump Short Relative operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for JR operation
    }

    private int Execute_JR(DecodedOperation operation)
    {
        // Jump Relative operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for JR operation
    }

    private int Execute_CALL(DecodedOperation operation)
    {
        // Call operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CALL operation
    }

    private int Execute_CALLR_s8(DecodedOperation operation)
    {
        // Call Relative Short operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CALLR_s8 operation
    }

    private int Execute_CALLR(DecodedOperation operation)
    {
        // Call Relative operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CALLR operation
    }

    private int Execute_RET(DecodedOperation operation)
    {
        // Return operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RET operation
    }

    private int Execute_RETI(DecodedOperation operation)
    {
        // Return from Interrupt operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RETI operation
    }

    private int Execute_RETN(DecodedOperation operation)
    {
        // Return from Non-Maskable Interrupt operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RETN operation
    }

    private int Execute_DJNZ(DecodedOperation operation)
    {
        // Decrement and Jump if Not Zero operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for DJNZ operation
    }

    private int Execute_JANZ(DecodedOperation operation)
    {
        // Jump if A is Not Zero operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for JANZ operation
    }

    private int Execute_RST(DecodedOperation operation)
    {
        // Restart operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for RST operation
    }

    private int Execute_SCF(DecodedOperation operation)
    {
        // Set Carry Flag operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for SCF operation
    }

    private int Execute_CCF(DecodedOperation operation)
    {
        // Complement Carry Flag operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for CCF operation
    }

    private int Execute_EI(DecodedOperation operation)
    {
        // Enable Interrupts operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for EI operation
    }

    private int Execute_DI(DecodedOperation operation)
    {
        // Disable Interrupts operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for DI operation
    }

    private int Execute_IM1(DecodedOperation operation)
    {
        // Interrupt Mode 1 operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for IM1 operation
    }

    private int Execute_IM2(DecodedOperation operation)
    {
        // Interrupt Mode 2 operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for IM2 operation
    }

    private int Execute_HALT(DecodedOperation operation)
    {
        // HALT operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for HALT operation
    }

    private int Execute_LD_I_NN(DecodedOperation operation)
    {
        // Load Immediate into I operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for LD_I_NN operation
    }

    private int Execute_LD_R_A(DecodedOperation operation)
    {
        // Load A into R operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for LD_R_A operation
    }

    private int Execute_LD_A_R(DecodedOperation operation)
    {
        // Load R into A operation logic would go here
        return operation.FetchCycles + 1; // Example cycle count for LD_A_R operation
    }

}