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

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operandRead.Cycles;

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, operandRead.Value);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Exchange
    private int Execute_EX(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("EX requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead1 = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operandRead1.Cycles;
        MemoryResult operandRead2 = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operandRead2.Cycles;

        MemoryResult operandWrite1 = WritebackOperand(operation.Operand1, operation.OperandSize, operandRead2.Value);
        cycles += operandWrite1.Cycles;
        MemoryResult operandWrite2 = WritebackOperand(operation.Operand2, operation.OperandSize, operandRead1.Value);
        cycles += operandWrite2.Cycles;

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

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operandRead.Cycles;

        uint value = operandRead.Value;

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

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, value);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Push to stack
    private int Execute_PUSH(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("PUSH requires one operand");
        }

        if (operation.OperandSize != Constants.OperandSize.DWord)
        {
            throw new ArgumentException("PUSH only operates on DWord operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operandRead.Cycles;
        uint stackPointer = Registers.GetRegister(Constants.RegisterTargets.SP, Constants.OperandSize.DWord);

        // Pre-decrement for PUSH
        stackPointer -= 4;

        // Write value to memory
        MemoryResult operandWrite = WriteMemory(stackPointer, Constants.OperandSize.DWord, operandRead.Value);
        cycles += operandWrite.Cycles;

        // Writeback SP
        Registers.SetRegister(Constants.RegisterTargets.SP, Constants.OperandSize.DWord, stackPointer);

        return cycles;
    }

    // Pop from stack
    private int Execute_POP(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("POP requires one operand");
        }

        if (operation.OperandSize != Constants.OperandSize.DWord)
        {
            throw new ArgumentException("POP only operates on DWord operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        uint stackPointer = Registers.GetRegister(Constants.RegisterTargets.SP, Constants.OperandSize.DWord);

        // Read value from memory
        MemoryResult operandRead = ReadMemory(stackPointer, Constants.OperandSize.DWord);
        cycles += operandRead.Cycles;

        uint value = operandRead.Value;

        // Write to destination
        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, value);
        cycles += operandWrite.Cycles;

        // Post-increment for POP
        stackPointer += 4;

        // Writeback SP
        Registers.SetRegister(Constants.RegisterTargets.SP, Constants.OperandSize.DWord, stackPointer);

        return cycles;
    }

    // In/Out
    private int Execute_IN_OUT(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("IN/OUT requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        // OUT, Operand1 is indirect
        if (operation.Operand1.Indirect)
        {
            MemoryResult valueRead = GetOperandValue(operation.Operand2, operation.OperandSize);
            cycles += valueRead.Cycles;

            // Use GetOperandValue internal logic directly, since this is the only instruction which uses I/O
            uint port = GetEffectiveAddress(operation.Operand1);
            MemoryResult ioWrite = WriteMemory(port, operation.OperandSize, valueRead.Value, useIO: true);
            cycles += ioWrite.Cycles;
        }
        // IN, Operand2 is indirect
        else if (operation.Operand2.Indirect)
        {
            uint port = GetEffectiveAddress(operation.Operand2);
            MemoryResult ioRead = ReadMemory(port, operation.OperandSize, useIO: true);
            cycles += ioRead.Cycles;

            MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, ioRead.Value);
            cycles += operandWrite.Cycles;
        }
        // Some mysterious third thing that means I need to debug the decoder
        else
        {
            throw new Exception("IN/OUT requires one indirect operand");
        }

        return cycles;
    }

    // Load and Increment
    private int Execute_LDI(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("LDI requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_LD(operation.OperandSize, increment: true);

        return cycles;
    }

    // Load, Increment, and Repeat
    private int Execute_LDIR(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("LDIR requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_LD(operation.OperandSize, increment: true);
        cycles += Internal_Block_Loop(operation);

        return cycles;
    }

    // Load and Decrement
    private int Execute_LDD(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("LDD requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_LD(operation.OperandSize, increment: false);

        return cycles;
    }

    // Load, Decrement, and Repeat
    private int Execute_LDDR(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("LDDR requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_LD(operation.OperandSize, increment: false);
        cycles += Internal_Block_Loop(operation);

        return cycles;
    }

    // Compare and Increment
    private int Execute_CPI(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("CPI requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_CP(operation.OperandSize, increment: true);

        return cycles;
    }

    // Compare, Increment, and Repeat
    private int Execute_CPIR(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("CPIR requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_CP(operation.OperandSize, increment: true);
        cycles += Internal_Block_Loop(operation);

        return cycles;
    }

    // Compare and Decrement
    private int Execute_CPD(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("CPD requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_CP(operation.OperandSize, increment: false);

        return cycles;
    }

    // Compare, Decrement, and Repeat
    private int Execute_CPDR(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("CPD requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_CP(operation.OperandSize, increment: false);
        cycles += Internal_Block_Loop(operation);

        return cycles;
    }

    // Test and Increment
    private int Execute_TSI(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("TSI requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_TST(operation.OperandSize, increment: true);

        return cycles;
    }

    // Test, Increment, and Repeat
    private int Execute_TSIR(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("TSIR requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_TST(operation.OperandSize, increment: true);
        cycles += Internal_Block_Loop(operation);

        return cycles;
    }

    // Test and Decrement
    private int Execute_TSD(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("TSD requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_TST(operation.OperandSize, increment: false);

        return cycles;
    }

    // Test, Decrement, and Repeat
    private int Execute_TSDR(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("TSDR requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        cycles += Internal_Block_TST(operation.OperandSize, increment: false);
        cycles += Internal_Block_Loop(operation);

        return cycles;
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

    // Add
    private int Execute_ADD(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("ADD requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operand1Read.Value;
                byte b = (byte)operand2Read.Value;

                result = (byte)(a + b);

                flagState.Carry = Helpers.BitHelper.WillAdditionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillAdditionOverflow(a, b);
                flagState.HalfCarry = Helpers.BitHelper.WillAdditionHalfCarry(a, b);
                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operand1Read.Value;
                ushort b = (ushort)operand2Read.Value;

                result = (ushort)(a + b);

                flagState.Carry = Helpers.BitHelper.WillAdditionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillAdditionOverflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operand1Read.Value;
                uint b = operand2Read.Value;

                result = a + b;

                flagState.Carry = Helpers.BitHelper.WillAdditionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillAdditionOverflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_ADD is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Subtract = false;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Add with Carry
    private int Execute_ADC(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("ADC requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operand1Read.Value;
                byte b = (byte)operand2Read.Value;
                if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

                result = (byte)(a + b);

                flagState.Carry = Helpers.BitHelper.WillAdditionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillAdditionOverflow(a, b);
                flagState.HalfCarry = Helpers.BitHelper.WillAdditionHalfCarry(a, b);
                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operand1Read.Value;
                ushort b = (ushort)operand2Read.Value;
                if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

                result = (ushort)(a + b);

                flagState.Carry = Helpers.BitHelper.WillAdditionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillAdditionOverflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operand1Read.Value;
                uint b = operand2Read.Value;
                if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

                result = a + b;

                flagState.Carry = Helpers.BitHelper.WillAdditionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillAdditionOverflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_ADC is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Subtract = false;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Subtract
    private int Execute_SUB(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("SUB requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operand1Read.Value;
                byte b = (byte)operand2Read.Value;

                result = (byte)(a - b);

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.HalfCarry = Helpers.BitHelper.WillSubtractionHalfCarry(a, b);
                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operand1Read.Value;
                ushort b = (ushort)operand2Read.Value;

                result = (ushort)(a - b);

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operand1Read.Value;
                uint b = operand2Read.Value;

                result = a - b;

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_SUB is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Subtract = true;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Subtract with Carry
    private int Execute_SBC(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("SBC requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operand1Read.Value;
                byte b = (byte)operand2Read.Value;
                if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

                result = (byte)(a - b);

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.HalfCarry = Helpers.BitHelper.WillSubtractionHalfCarry(a, b);
                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operand1Read.Value;
                ushort b = (ushort)operand2Read.Value;
                if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

                result = (ushort)(a - b);

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operand1Read.Value;
                uint b = operand2Read.Value;
                if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

                result = a - b;

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_SBC is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Subtract = true;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Compare
    private int Execute_CP(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("CP requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        cycles += Internal_CP(operand1Read.Value, operand2Read.Value, operation.OperandSize);

        // No writeback

        return cycles;
    }

    // Increment
    private int Execute_INC(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("INC requires one operand");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operandRead.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operandRead.Value;
                byte b = 1;
                result = (byte)(a + b);

                flagState.Carry = Helpers.BitHelper.WillAdditionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillAdditionOverflow(a, b);
                flagState.HalfCarry = Helpers.BitHelper.WillAdditionHalfCarry(a, b);
                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operandRead.Value;
                ushort b = 1;
                result = (ushort)(a + b);

                flagState.Carry = Helpers.BitHelper.WillAdditionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillAdditionOverflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operandRead.Value;
                uint b = 1;
                result = a + b;

                flagState.Carry = Helpers.BitHelper.WillAdditionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillAdditionOverflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_INC is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Subtract = false;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Decrement
    private int Execute_DEC(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("DEC requires one operand");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operand1Read.Value;
                byte b = 1;
                result = (byte)(a - b);

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.HalfCarry = Helpers.BitHelper.WillSubtractionHalfCarry(a, b);
                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operand1Read.Value;
                ushort b = 1;
                result = (ushort)(a - b);

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operand1Read.Value;
                uint b = 1;

                result = a - b;

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_SUB is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Subtract = true;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Decimal Adjust A
    // Implementation taken from "The Undocumented Z80 Documented" by Sean Young
    private int Execute_DAA(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("DAA requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;
        ushort a = (ushort)Registers.GetRegister(Constants.RegisterTargets.A, Constants.OperandSize.Word);

        ALUFlagState flagState = GetALUFlags();

        int t = 0;

        if (flagState.HalfCarry || ((a & 0xF) > 9))
        {
            t++;
        }

        if (flagState.Carry || (a > 0x99))
        {
            t += 2;
            flagState.Carry = true;
        }


        if (flagState.Subtract && !flagState.HalfCarry)
        {
            flagState.HalfCarry = false;
        }
        else
        {
            if (flagState.Subtract && flagState.HalfCarry)
            {
                flagState.HalfCarry = (a & 0x0F) < 6;
            }
            else
            {
                flagState.HalfCarry = (a & 0x0F) >= 0x0A;
            }
        }

        switch (t)
        {
            case 1:
            {
                a += (byte)(flagState.Subtract ? 0xFA : 0x06); // -6:6
                break;
            }
            case 2:
            {
                a += (byte)(flagState.Subtract ? 0xA0 : 0x60); // -0x60:0x60
                break;
            }
            case 3:
            {
                a += (byte)(flagState.Subtract ? 0x9A : 0x66); // -0x66:0x66
                break;
            }
        }

        flagState.Sign = (a & 0x80) != 0;
        flagState.Zero = a == 0;
        flagState.ParityOverflow = Helpers.BitHelper.IsParityEven(a);

        UpdateALUFlags(flagState);
        Registers.SetRegister(Constants.RegisterTargets.A, Constants.OperandSize.Word, a);

        return cycles;
    }

    // Negate
    // Implemented as 0 - x for flags
    private int Execute_NEG(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("NEG requires one operand");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operandRead.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operandRead.Value;

                result = (byte)-a;

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(0, a);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(0, a);
                flagState.HalfCarry = Helpers.BitHelper.WillSubtractionHalfCarry(0, a);
                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operandRead.Value;

                result = (ushort)-a;

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(0, a);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(0, a);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operandRead.Value;

                result = (uint)-a;

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(0, a);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(0, a);
                flagState.HalfCarry = false; // Undefined
                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_NEG is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Subtract = true;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Sign Extend
    private int Execute_EXT(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("EXT requires one operand");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operandRead.Cycles;

        uint result = 0;

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operandRead.Value;
                result = (ushort)Helpers.BitHelper.SignExtend(a, 8);
                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operandRead.Value;
                result = Helpers.BitHelper.SignExtend(a, 16);
                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_EXT is not implemented for operand size {operation.OperandSize}");
            }
        }

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Multiply
    private int Execute_MLT(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("MLT requires one operand");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operandRead.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Word:
            {
                int multiplicand = (int)(operandRead.Value & 0xFF);
                int multiplier = (int)((operandRead.Value >> 8) & 0xFF);

                int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
                cycles += cyclesPerIteration * 8; // 8 Iterations for 8*8 mult
                cycles += Helpers.BitHelper.NumberOfOnes((uint)multiplicand); // Roughly number of adds/subs needed

                result = (ushort)(multiplicand * multiplier);

                flagState.Carry = result > byte.MaxValue;
                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                int multiplicand = (int)(operandRead.Value & 0xFFFF);
                int multiplier = (int)((operandRead.Value >> 16) & 0xFFFF);

                int cyclesPerIteration = 4 + Config.DWordALUCost; // Branch, Shift, Loop Overhead
                cycles += cyclesPerIteration * 16; // 16 iterations for 16*16 mult
                cycles += Helpers.BitHelper.NumberOfOnes((uint)multiplicand); // Roughly number of adds/subs needed

                result = (uint)(multiplicand * multiplier);

                flagState.Carry = result > ushort.MaxValue;
                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_MLT is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.HalfCarry = false;
        flagState.Subtract = false;
        flagState.Zero = result == 0; // Technically undefined, probably want to define. Seems useful.
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Divide
    private int Execute_DIV(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("DIV requires one operand");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operandRead.Cycles;

        uint result = 0;

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Word:
            {
                uint dividend = (operandRead.Value >> 8) & 0xFF;
                uint divisor = operandRead.Value & 0xFF;

                // Decision
                cycles += 1;
                if (divisor == 0)
                {
                    Registers.SetFlag(Constants.FlagMasks.ParityOverflow, true);
                }
                else
                {
                    Registers.SetFlag(Constants.FlagMasks.ParityOverflow, false);

                    int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
                    cycles += cyclesPerIteration * 8; // 8 Iterations for 8/8 div
                    cycles += Helpers.BitHelper.NumberOfOnes(dividend); // Roughly number of adds/subs needed

                    result = (byte)(dividend / divisor); // Quotient in lower half
                    result |= (uint)(byte)(dividend % divisor) << 8; // Remainder in upper half
                }

                break;
            }

            case Constants.OperandSize.DWord:
            {
                uint dividend = (operandRead.Value >> 16) & 0xFFFF;
                uint divisor = operandRead.Value & 0xFFFF;

                // Decision
                cycles += 1;
                if (divisor == 0)
                {
                    Registers.SetFlag(Constants.FlagMasks.ParityOverflow, true);
                }
                else
                {
                    Registers.SetFlag(Constants.FlagMasks.ParityOverflow, false);

                    int cyclesPerIteration = 4 + Config.DWordALUCost; // Branch, Shift, Loop Overhead
                    cycles += cyclesPerIteration * 16; // 16 Iterations for 16/16 div
                    cycles += Helpers.BitHelper.NumberOfOnes(dividend); // Roughly number of adds/subs needed

                    result = (ushort)(dividend / divisor); // Quotient in lower half
                    result |= (uint)(ushort)(dividend % divisor) << 16; // Remainder in upper half
                }

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_DIV is not implemented for operand size {operation.OperandSize}");
            }
        }

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Signed Divide
    private int Execute_SDIV(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("SDIV requires one operand");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operandRead = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operandRead.Cycles;

        uint result = 0;

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Word:
            {
                int dividend = (int)((operandRead.Value >> 8) & 0xFF);
                int divisor = (int)(operandRead.Value & 0xFF);

                // Detect signs, detect 0, negate
                cycles += 4;

                if (divisor == 0)
                {
                    Registers.SetFlag(Constants.FlagMasks.ParityOverflow, true);
                }
                else
                {
                    Registers.SetFlag(Constants.FlagMasks.ParityOverflow, false);

                    int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
                    cycles += cyclesPerIteration * 16; // 16 Iterations for 16/16 div
                    cycles += Helpers.BitHelper.NumberOfOnes((uint)dividend); // Roughly number of adds/subs needed

                    // Negate result decision
                    cycles++;

                    result = (byte)(dividend / divisor); // Quotient in lower half
                    result |= (uint)(byte)(dividend % divisor) << 8; // Remainder in upper half
                }

                break;
            }

            case Constants.OperandSize.DWord:
            {
                int dividend = (int)((operandRead.Value >> 16) & 0xFFFF);
                int divisor = (int)(operandRead.Value & 0xFFFF);

                // Detect signs, detect 0, negate
                cycles += 4;

                if (divisor == 0)
                {
                    Registers.SetFlag(Constants.FlagMasks.ParityOverflow, true);
                }
                else
                {
                    Registers.SetFlag(Constants.FlagMasks.ParityOverflow, false);

                    int cyclesPerIteration = 4 + Config.DWordALUCost; // Branch, Shift, Loop Overhead
                    cycles += cyclesPerIteration * 8; // 8 Iterations for 8/8 div
                    cycles += Helpers.BitHelper.NumberOfOnes((uint)dividend); // Roughly number of adds/subs needed

                    // Negate result decision
                    cycles++;

                    result = (ushort)(dividend / divisor); // Quotient in lower half
                    result |= (uint)(ushort)(dividend % divisor) << 16; // Remainder in upper half
                }

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_SDIV is not implemented for operand size {operation.OperandSize}");
            }
        }

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // AND
    private int Execute_AND(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("AND requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operand1Read.Value;
                byte b = (byte)operand2Read.Value;

                result = (byte)(a & b);

                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operand1Read.Value;
                ushort b = (ushort)operand2Read.Value;

                result = (ushort)(a & b);

                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operand1Read.Value;
                uint b = operand2Read.Value;

                result = a & b;

                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_AND is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Carry = false;
        flagState.ParityOverflow = Helpers.BitHelper.IsParityEven(result);
        flagState.HalfCarry = true;
        flagState.Subtract = false;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // OR
    private int Execute_OR(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("OR requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operand1Read.Value;
                byte b = (byte)operand2Read.Value;

                result = (byte)(a | b);

                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operand1Read.Value;
                ushort b = (ushort)operand2Read.Value;

                result = (ushort)(a | b);

                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operand1Read.Value;
                uint b = operand2Read.Value;

                result = a | b;

                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_OR is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Carry = false;
        flagState.ParityOverflow = Helpers.BitHelper.IsParityEven(result);
        flagState.HalfCarry = false;
        flagState.Subtract = false;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // XOR
    private int Execute_XOR(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("XOR requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operand1Read.Value;
                byte b = (byte)operand2Read.Value;

                result = (byte)(a ^ b);

                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operand1Read.Value;
                ushort b = (ushort)operand2Read.Value;

                result = (ushort)(a ^ b);

                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operand1Read.Value;
                uint b = operand2Read.Value;

                result = a ^ b;

                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_XOR is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Carry = false;
        flagState.ParityOverflow = Helpers.BitHelper.IsParityEven(result);
        flagState.HalfCarry = false;
        flagState.Subtract = false;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // TST
    private int Execute_TST(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("TST requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        cycles += Internal_TST(operand1Read.Value, operand2Read.Value, operation.OperandSize);

        return cycles;
    }

    // CPL
    private int Execute_CPL(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is not null)
        {
            throw new ArgumentException("CPL requires one operand");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                byte a = (byte)operand1Read.Value;

                result = (byte)(a ^ 0xFF);

                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                ushort a = (ushort)operand1Read.Value;

                result = (ushort)(a ^ 0xFFFF);

                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                uint a = operand1Read.Value;

                result = a ^ 0xFFFFFFFF;

                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_CPL is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.Carry = false;
        flagState.ParityOverflow = Helpers.BitHelper.IsParityEven(result);
        flagState.HalfCarry = false;
        flagState.Subtract = false;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Test bit
    private int Execute_BIT(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("BIT requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint bit = operand2Read.Value;
        ALUFlagState flagState = GetALUFlags();

        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                bit &= 0b111;
                break;
            }

            case Constants.OperandSize.Word:
            {
                bit &= 0b1111;
                break;
            }

            case Constants.OperandSize.DWord:
            {
                bit &= 0b11111;
                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_CPL is not implemented for operand size {operation.OperandSize}");
            }
        }

        flagState.HalfCarry = false;
        flagState.Subtract = false;
        flagState.Zero = (((int)operand1Read.Value >> (int)bit) & 1) == 0;
        UpdateALUFlags(flagState);

        return cycles;
    }

    // Set bit
    private int Execute_SET(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("SET requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint bit = operand2Read.Value;
        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                bit &= 0b111;
                break;
            }

            case Constants.OperandSize.Word:
            {
                bit &= 0b1111;
                break;
            }

            case Constants.OperandSize.DWord:
            {
                bit &= 0b11111;
                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_CPL is not implemented for operand size {operation.OperandSize}");
            }
        }

        uint result = operand1Read.Value;
        result |= (uint)(1 << (int)bit);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
    }

    // Reset bit
    private int Execute_RES(DecodedOperation operation)
    {
        if (operation.Operand1 is null || operation.Operand2 is null)
        {
            throw new ArgumentException("RES requires two operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        MemoryResult operand1Read = GetOperandValue(operation.Operand1, operation.OperandSize);
        cycles += operand1Read.Cycles;

        MemoryResult operand2Read = GetOperandValue(operation.Operand2, operation.OperandSize);
        cycles += operand2Read.Cycles;

        uint bit = operand2Read.Value;
        switch (operation.OperandSize)
        {
            case Constants.OperandSize.Byte:
            {
                bit &= 0b111;
                break;
            }

            case Constants.OperandSize.Word:
            {
                bit &= 0b1111;
                break;
            }

            case Constants.OperandSize.DWord:
            {
                bit &= 0b11111;
                break;
            }

            default:
            {
                throw new ArgumentException($"Execute_CPL is not implemented for operand size {operation.OperandSize}");
            }
        }

        uint result = operand1Read.Value;
        result &= ~(uint)(1 << (int)bit);

        MemoryResult operandWrite = WritebackOperand(operation.Operand1, operation.OperandSize, result);
        cycles += operandWrite.Cycles;

        return cycles;
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

    // Set Carry Flag
    private int Execute_SCF(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("SCF requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        Registers.SetFlag(Constants.FlagMasks.Carry, true);

        return cycles;
    }

    // Complement Carry Flag
    private int Execute_CCF(DecodedOperation operation)
    {
        if (operation.Operand1 is not null || operation.Operand2 is not null)
        {
            throw new ArgumentException("CCF requires no operands");
        }

        int cycles = operation.FetchCycles + Config.BaseInstructionCost;

        bool carry = Registers.GetFlag(Constants.FlagMasks.Carry);
        Registers.SetFlag(Constants.FlagMasks.Carry, !carry);

        return cycles;
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

    private int Internal_Block_LD(Constants.OperandSize size, bool increment)
    {
        int cycles = 0;

        uint bc = Registers.GetRegister(Constants.RegisterTargets.BC, Constants.OperandSize.DWord);
        uint de = Registers.GetRegister(Constants.RegisterTargets.DE, Constants.OperandSize.DWord);
        uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.OperandSize.DWord);

        MemoryResult readHL = ReadMemory(hl, size);
        cycles += readHL.Cycles;
        MemoryResult writeDE = WriteMemory(de, size, readHL.Value);
        cycles += writeDE.Cycles;

        cycles++; // Extra cycle for increment/decrement logic

        uint adjustAmount = size switch
        {
            Constants.OperandSize.Byte => 1,
            Constants.OperandSize.Word => 2,
            Constants.OperandSize.DWord => 4,
            _ => throw new Exception($"Internal_Block_LD is not implemented for OperandSize {size}")
        };

        if (increment)
        {
            hl += adjustAmount;
            de += adjustAmount;
        }
        else
        {
            hl -= adjustAmount;
            de -= adjustAmount;
        }

        // Decrement counter and update flags
        bc--;

        ALUFlagState flagState = GetALUFlags();
        flagState.HalfCarry = false;
        flagState.Subtract = false;
        flagState.ParityOverflow = bc != 0;
        UpdateALUFlags(flagState);

        // Extra cycle for register writeback
        cycles++;

        Registers.SetRegister(Constants.RegisterTargets.BC, Constants.OperandSize.DWord, bc);
        Registers.SetRegister(Constants.RegisterTargets.DE, Constants.OperandSize.DWord, de);
        Registers.SetRegister(Constants.RegisterTargets.HL, Constants.OperandSize.DWord, hl);

        return cycles;
    }

    private int Internal_Block_CP(Constants.OperandSize size, bool increment)
    {
        int cycles = 0;

        ushort a = (ushort)Registers.GetRegister(Constants.RegisterTargets.A, Constants.OperandSize.Word);
        uint bc = Registers.GetRegister(Constants.RegisterTargets.BC, Constants.OperandSize.DWord);
        uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.OperandSize.DWord);

        MemoryResult readHL = ReadMemory(hl, size);
        cycles += readHL.Cycles;

        cycles += Internal_CP(a, readHL.Value, size);

        cycles++; // Extra cycle for increment/decrement logic

        uint adjustAmount = size switch
        {
            Constants.OperandSize.Byte => 1,
            Constants.OperandSize.Word => 2,
            Constants.OperandSize.DWord => 4,
            _ => throw new Exception($"Block_CP is not implemented for OperandSize {size}")
        };

        if (increment)
        {
            hl += adjustAmount;
        }
        else
        {
            hl -= adjustAmount;
        }

        // Decrement counter and update flags
        bc--;

        ALUFlagState flagState = GetALUFlags();
        flagState.ParityOverflow = bc != 0;
        UpdateALUFlags(flagState);

        // Extra cycle for register writeback
        cycles++;

        Registers.SetRegister(Constants.RegisterTargets.BC, Constants.OperandSize.DWord, bc);
        Registers.SetRegister(Constants.RegisterTargets.HL, Constants.OperandSize.DWord, hl);

        return cycles;
    }

    private int Internal_Block_TST(Constants.OperandSize size, bool increment)
    {
        int cycles = 0;

        ushort a = (ushort)Registers.GetRegister(Constants.RegisterTargets.A, Constants.OperandSize.Word);
        uint bc = Registers.GetRegister(Constants.RegisterTargets.BC, Constants.OperandSize.DWord);
        uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.OperandSize.DWord);

        MemoryResult readHL = ReadMemory(hl, size);
        cycles += readHL.Cycles;

        cycles += Internal_TST(a, readHL.Value, size);

        cycles++; // Extra cycle for increment/decrement logic

        uint adjustAmount = size switch
        {
            Constants.OperandSize.Byte => 1,
            Constants.OperandSize.Word => 2,
            Constants.OperandSize.DWord => 4,
            _ => throw new Exception($"Block_TST is not implemented for OperandSize {size}")
        };

        if (increment)
        {
            hl += adjustAmount;
        }
        else
        {
            hl -= adjustAmount;
        }

        // Decrement counter and update flags
        bc--;

        ALUFlagState flagState = GetALUFlags();
        flagState.ParityOverflow = bc != 0;
        UpdateALUFlags(flagState);

        // Extra cycle for register writeback
        cycles++;

        Registers.SetRegister(Constants.RegisterTargets.BC, Constants.OperandSize.DWord, bc);
        Registers.SetRegister(Constants.RegisterTargets.HL, Constants.OperandSize.DWord, hl);

        return cycles;
    }

    private int Internal_CP(uint a, uint b, Constants.OperandSize size)
    {
        int cycles = 0;
        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (size)
        {
            case Constants.OperandSize.Byte:
            {
                a = (byte)a;
                b = (byte)b;

                result = (byte)(a - b);

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap((byte)a, (byte)b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow((byte)a, (byte)b);
                flagState.HalfCarry = Helpers.BitHelper.WillSubtractionHalfCarry((byte)a, (byte)b);
                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                a = (ushort)a;
                b = (ushort)b;

                result = (ushort)(a - b);

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap((ushort)a, (ushort)b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow((ushort)a, (ushort)b);
                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                result = a - b;

                flagState.Carry = Helpers.BitHelper.WillSubtractionWrap(a, b);
                flagState.ParityOverflow = Helpers.BitHelper.WillSubtractionUnderflow(a, b);
                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Internal_CP is not implemented for operand size {size}");
            }
        }

        flagState.Subtract = true;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        return cycles;
    }

    private int Internal_TST(uint a, uint b, Constants.OperandSize size)
    {
        int cycles = 0;
        uint result = 0;
        ALUFlagState flagState = GetALUFlags();

        switch (size)
        {
            case Constants.OperandSize.Byte:
            {
                a = (byte)a;
                b = (byte)b;

                result = (byte)(a ^ b);

                flagState.Sign = (result & 0x80) != 0;

                break;
            }

            case Constants.OperandSize.Word:
            {
                a = (ushort)a;
                b = (ushort)b;

                result = (ushort)(a ^ b);

                flagState.Sign = (result & 0x8000) != 0;

                break;
            }

            case Constants.OperandSize.DWord:
            {
                cycles += Config.DWordALUCost;

                result = a ^ b;

                flagState.Sign = (result & 0x80000000) != 0;

                break;
            }

            default:
            {
                throw new ArgumentException($"Internal_TST is not implemented for operand size {size}");
            }
        }

        flagState.Carry = false;
        flagState.ParityOverflow = Helpers.BitHelper.IsParityEven(result);
        flagState.HalfCarry = false;
        flagState.Subtract = false;
        flagState.Zero = result == 0;
        UpdateALUFlags(flagState);

        return cycles;
    }
    private int Internal_Block_Loop(DecodedOperation operation)
    {
        // If PV == 1 (BC != 0), continue
        if (Registers.GetFlag(Constants.FlagMasks.ParityOverflow))
        {
            // Undo PC increment (so we can re-fetch)
            uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.OperandSize.DWord);
            pc -= (uint)operation.Opcode.Count;
            Registers.SetRegister(Constants.RegisterTargets.PC, Constants.OperandSize.DWord, pc);

            // Additional cycle to do the PC decrement
            return 1;
        }

        // Otherwise, done. Advance normally.
        return 0;
    }
}