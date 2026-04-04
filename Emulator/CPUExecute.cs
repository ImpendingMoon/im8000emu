using im8000emu.Helpers;

namespace im8000emu.Emulator;

// Implements methods to execute each operation
internal partial class CPU
{
	private int Execute_None(in DecodedOperation operation)
	{
		// Here for completeness. None should be caught in decoder.
		return operation.FetchCycles;
	}

	private int Execute_Interrupt(in DecodedOperation operation)
	{
		Registers.SetFlag(Constants.FlagMasks.EnableInterrupts, false);
		Registers.SetFlag(Constants.FlagMasks.EnableInterruptsSave, false);

		if (_interruptMode == 1)
		{
			return Internal_ServiceInterrupt(1);
		}

		byte number = _interruptBus.Acknowledge(false);
		return Internal_ServiceInterrupt(number);
	}

	private int Execute_NonMaskableInterrupt(in DecodedOperation operation)
	{
		// Save IFF2
		bool iff1 = Registers.GetFlag(Constants.FlagMasks.EnableInterrupts);
		Registers.SetFlag(Constants.FlagMasks.EnableInterruptsSave, iff1);
		Registers.SetFlag(Constants.FlagMasks.EnableInterrupts, false);

		_interruptBus.Acknowledge(true);
		return Internal_ServiceInterrupt(2);
	}

	// Do Nothing
	private int Execute_HaltState(in DecodedOperation operation)
	{
		// CPU is halted; keep executing NOPs until an interrupt wakes us
		return 4;
	}

	// Load
	private int Execute_LD(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("LD requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operandRead.Cycles;

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, operandRead.Value);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Exchange
	private int Execute_EX(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("EX requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead1 = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operandRead1.Cycles;
		MemoryResult operandRead2 = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operandRead2.Cycles;

		MemoryResult operandWrite1 = WritebackOperand(
			operation.Operand1!.Value,
			operation.DataSize,
			operandRead2.Value
		);
		cycles += operandWrite1.Cycles;
		MemoryResult operandWrite2 = WritebackOperand(
			operation.Operand2!.Value,
			operation.DataSize,
			operandRead1.Value
		);
		cycles += operandWrite2.Cycles;

		return cycles;
	}

	// Exchange with Alternate
	private int Execute_EX_Alt(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("EX_Alt requires one operand");
		}

		if (operation.Operand1!.Value.Target is null || operation.Operand1!.Value.Indirect)
		{
			throw new EmulatorFaultException("EX_Alt can only operate on register targets");
		}

		if (operation.DataSize == Constants.DataSize.Byte)
		{
			throw new IllegalInstructionException(
				operation.BaseAddress,
				"EX_Alt cannot be used with byte operands",
				CaptureContext()
			);
		}

		ExchangeWithAlternate(operation.Operand1!.Value.Target.Value, operation.DataSize);

		return operation.FetchCycles + 1;
	}

	// Exchange Primary with Alternate
	private int Execute_EXX(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("EXX requires no operands");
		}

		ExchangeWithAlternate(Constants.RegisterTargets.BC, Constants.DataSize.DWord);
		ExchangeWithAlternate(Constants.RegisterTargets.DE, Constants.DataSize.DWord);
		ExchangeWithAlternate(Constants.RegisterTargets.HL, Constants.DataSize.DWord);

		return operation.FetchCycles + 1;
	}

	// Exchange Index with Alternate
	private int Execute_EXI(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("EXI requires no operands");
		}

		ExchangeWithAlternate(Constants.RegisterTargets.IX, Constants.DataSize.DWord);
		ExchangeWithAlternate(Constants.RegisterTargets.IY, Constants.DataSize.DWord);
		ExchangeWithAlternate(Constants.RegisterTargets.SP, Constants.DataSize.DWord);

		return operation.FetchCycles + 1;
	}

	// Exchange Halves
	private int Execute_EXH(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("EXH requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operandRead.Cycles;

		uint value = operandRead.Value;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte temp = (byte)(value >> 4);
				temp |= (byte)(value << 4);
				value = temp;
				break;
			}
			case Constants.DataSize.Word:
			{
				ushort temp = (ushort)(value >> 8);
				temp |= (ushort)(value << 8);
				value = temp;
				break;
			}
			case Constants.DataSize.DWord:
			{
				uint temp = value >> 16;
				temp |= value << 16;
				value = temp;
				break;
			}
		}

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, value);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Push to stack
	private int Execute_PUSH(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("PUSH requires one operand");
		}

		if (operation.DataSize != Constants.DataSize.DWord)
		{
			throw new EmulatorFaultException("PUSH only operates on DWord operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operandRead.Cycles;

		cycles += Internal_Push(operandRead.Value);

		return cycles;
	}

	// Pop from stack
	private int Execute_POP(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("POP requires one operand");
		}

		if (operation.DataSize != Constants.DataSize.DWord)
		{
			throw new EmulatorFaultException("POP only operates on DWord operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult popRead = Internal_Pop();
		cycles += popRead.Cycles;

		// Write to destination
		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, popRead.Value);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// In/Out
	private int Execute_IN_OUT(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("IN/OUT requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		// OUT, Operand1 is indirect
		if (operation.Operand1!.Value.Indirect)
		{
			MemoryResult valueRead = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
			cycles += valueRead.Cycles;

			// Use GetOperandValue internal logic directly, since this is the only instruction which uses I/O
			uint port = GetEffectiveAddress(operation.Operand1!.Value);
			MemoryResult ioWrite = WriteMemory(port, operation.DataSize, valueRead.Value, true);
			cycles += ioWrite.Cycles;
		}
		// IN, Operand2 is indirect
		else if (operation.Operand2!.Value.Indirect)
		{
			uint port = GetEffectiveAddress(operation.Operand2!.Value);
			MemoryResult ioRead = ReadMemory(port, operation.DataSize, true);
			cycles += ioRead.Cycles;

			MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, ioRead.Value);
			cycles += operandWrite.Cycles;
		}
		// Some mysterious third thing that means I need to debug the decoder
		else
		{
			throw new ExecutionFaultException("IN/OUT requires one indirect operand", CaptureContext());
		}

		return cycles;
	}

	// Load and Increment
	private int Execute_LDI(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("LDI requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_LD(operation.DataSize, true);

		return cycles;
	}

	// Load, Increment, and Repeat
	private int Execute_LDIR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("LDIR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_LD(operation.DataSize, true);
		cycles += Internal_Block_Loop(operation);

		return cycles;
	}

	// Load and Decrement
	private int Execute_LDD(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("LDD requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_LD(operation.DataSize, false);

		return cycles;
	}

	// Load, Decrement, and Repeat
	private int Execute_LDDR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("LDDR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_LD(operation.DataSize, false);
		cycles += Internal_Block_Loop(operation);

		return cycles;
	}

	// Compare and Increment
	private int Execute_CPI(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("CPI requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_CP(operation.DataSize, true);

		return cycles;
	}

	// Compare, Increment, and Repeat
	private int Execute_CPIR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("CPIR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_CP(operation.DataSize, true);
		cycles += Internal_Block_Loop(operation);

		return cycles;
	}

	// Compare and Decrement
	private int Execute_CPD(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("CPD requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_CP(operation.DataSize, false);

		return cycles;
	}

	// Compare, Decrement, and Repeat
	private int Execute_CPDR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("CPDR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_CP(operation.DataSize, false);
		cycles += Internal_Block_Loop(operation);

		return cycles;
	}

	// Test and Increment
	private int Execute_TSI(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("TSI requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_TST(operation.DataSize, true);

		return cycles;
	}

	// Test, Increment, and Repeat
	private int Execute_TSIR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("TSIR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_TST(operation.DataSize, true);
		cycles += Internal_Block_Loop(operation);

		return cycles;
	}

	// Test and Decrement
	private int Execute_TSD(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("TSD requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_TST(operation.DataSize, false);

		return cycles;
	}

	// Test, Decrement, and Repeat
	private int Execute_TSDR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("TSDR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_TST(operation.DataSize, false);
		cycles += Internal_Block_Loop(operation);

		return cycles;
	}

	// Input and Increment
	private int Execute_INI(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("INI requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_IN(operation.DataSize, true);

		return cycles;
	}

	// Input, Increment, and Repeat
	private int Execute_INIR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("INIR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_IN(operation.DataSize, true);
		cycles += Internal_IO_Block_Loop(operation);

		return cycles;
	}

	// Input and Decrement
	private int Execute_IND(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("IND requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_IN(operation.DataSize, false);

		return cycles;
	}

	// Input, Decrement, and Repeat
	private int Execute_INDR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("INDR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_IN(operation.DataSize, false);
		cycles += Internal_IO_Block_Loop(operation);

		return cycles;
	}

	// Output and Increment
	private int Execute_OUTI(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("OUTI requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_OUT(operation.DataSize, true);

		return cycles;
	}

	// Output, Increment, and Repeat
	private int Execute_OTIR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("OTIR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_OUT(operation.DataSize, true);
		cycles += Internal_IO_Block_Loop(operation);

		return cycles;
	}

	// Output and Decrement
	private int Execute_OUTD(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("OUTD requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_OUT(operation.DataSize, false);

		return cycles;
	}

	// Output, Decrement, and Repeat
	private int Execute_OTDR(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("OTDR requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		cycles += Internal_Block_OUT(operation.DataSize, false);
		cycles += Internal_IO_Block_Loop(operation);

		return cycles;
	}

	// Add
	private int Execute_ADD(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("ADD requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operand1Read.Value;
				byte b = (byte)operand2Read.Value;

				result = (byte)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operand1Read.Value;
				ushort b = (ushort)operand2Read.Value;

				result = (ushort)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				uint a = operand1Read.Value;
				uint b = operand2Read.Value;

				result = a + b;

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_ADD is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Add with Carry
	private int Execute_ADC(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("ADC requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operand1Read.Value;
				byte b = (byte)operand2Read.Value;
				if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

				result = (byte)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operand1Read.Value;
				ushort b = (ushort)operand2Read.Value;
				if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

				result = (ushort)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				uint a = operand1Read.Value;
				uint b = operand2Read.Value;
				if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

				result = a + b;

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_ADC is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Subtract
	private int Execute_SUB(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("SUB requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operand1Read.Value;
				byte b = (byte)operand2Read.Value;

				result = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operand1Read.Value;
				ushort b = (ushort)operand2Read.Value;

				result = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				uint a = operand1Read.Value;
				uint b = operand2Read.Value;

				result = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_SUB is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = true;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Subtract with Carry
	private int Execute_SBC(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("SBC requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operand1Read.Value;
				byte b = (byte)operand2Read.Value;
				if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

				result = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operand1Read.Value;
				ushort b = (ushort)operand2Read.Value;
				if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

				result = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				uint a = operand1Read.Value;
				uint b = operand2Read.Value;
				if (Registers.GetFlag(Constants.FlagMasks.Carry)) { b++; }

				result = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_SBC is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = true;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Compare
	private int Execute_CP(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("CP requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		cycles += Internal_CP(operand1Read.Value, operand2Read.Value, operation.DataSize);

		// No writeback

		return cycles;
	}

	// Increment
	private int Execute_INC(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("INC requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operandRead.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operandRead.Value;
				byte b = 1;
				result = (byte)(a + b);

				// flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operandRead.Value;
				ushort b = 1;
				result = (ushort)(a + b);

				// flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				uint a = operandRead.Value;
				uint b = 1;
				result = a + b;

				// flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_INC is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Decrement
	private int Execute_DEC(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("DEC requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operand1Read.Value;
				byte b = 1;
				result = (byte)(a - b);

				// flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operand1Read.Value;
				ushort b = 1;
				result = (ushort)(a - b);

				// flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				uint a = operand1Read.Value;
				uint b = 1;

				result = a - b;

				// flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_DEC is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = true;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Decimal Adjust A
	// Implementation taken from "The Undocumented Z80 Documented" by Sean Young
	private int Execute_DAA(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("DAA requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;
		ushort a = (byte)Registers.GetRegister(Constants.RegisterTargets.A, Constants.DataSize.Byte);

		ALUFlagState flagState = GetALUFlags();

		int t = 0;

		if (flagState.HalfCarry || (a & 0xF) > 9)
		{
			t++;
		}

		if (flagState.Carry || a > 0x99)
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
		flagState.ParityOverflow = BitHelper.IsParityEven(a);

		UpdateALUFlags(flagState);
		Registers.SetRegister(Constants.RegisterTargets.A, Constants.DataSize.Byte, a);

		return cycles;
	}

	// Negate
	// Implemented as 0 - x for flags
	private int Execute_NEG(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("NEG requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operandRead.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operandRead.Value;

				result = (byte)-a;

				flagState.Carry = BitHelper.WillSubtractionWrap(0, a);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(0, a);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(0, a);
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operandRead.Value;

				result = (ushort)-a;

				flagState.Carry = BitHelper.WillSubtractionWrap(0, a);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(0, a);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				uint a = operandRead.Value;

				result = (uint)-a;

				flagState.Carry = BitHelper.WillSubtractionWrap(0, a);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(0, a);
				flagState.HalfCarry = false; // Undefined
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_NEG is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = true;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Sign Extend
	private int Execute_EXT(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("EXT requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operandRead.Cycles;

		uint result;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operandRead.Value;
				result = (ushort)BitHelper.SignExtend(a, 8);
				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				uint a = operandRead.Value;
				result = BitHelper.SignExtend(a, 16);
				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_EXT is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Multiply
	private int Execute_MLT(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("MLT requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operandRead.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
			{
				int multiplicand = (int)(operandRead.Value & 0xFF);
				int multiplier = (int)((operandRead.Value >> 8) & 0xFF);

				int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
				cycles += cyclesPerIteration * 8; // 8 Iterations for 8*8 mult
				cycles += BitHelper.NumberOfOnes((uint)multiplicand); // Roughly number of adds/subs needed

				result = (ushort)(multiplicand * multiplier);

				flagState.Carry = result > byte.MaxValue;
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				int multiplicand = (int)(operandRead.Value & 0xFFFF);
				int multiplier = (int)((operandRead.Value >> 16) & 0xFFFF);

				int cyclesPerIteration = 4 + Config.DWordALUCost; // Branch, Shift, Loop Overhead
				cycles += cyclesPerIteration * 16; // 16 iterations for 16*16 mult
				cycles += BitHelper.NumberOfOnes((uint)multiplicand); // Roughly number of adds/subs needed

				result = (uint)(multiplicand * multiplier);

				flagState.Carry = result > ushort.MaxValue;
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_MLT is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = result == 0; // Technically undefined, probably want to define. Seems useful.
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Divide
	private int Execute_DIV(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("DIV requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operandRead.Cycles;

		uint result = 0;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
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
					cycles += BitHelper.NumberOfOnes(dividend); // Roughly number of adds/subs needed

					result = (byte)(dividend / divisor); // Quotient in lower half
					result |= (uint)(byte)(dividend % divisor) << 8; // Remainder in upper half
				}

				break;
			}

			case Constants.DataSize.DWord:
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
					cycles += BitHelper.NumberOfOnes(dividend); // Roughly number of adds/subs needed

					result = (ushort)(dividend / divisor); // Quotient in lower half
					result |= (uint)(ushort)(dividend % divisor) << 16; // Remainder in upper half
				}

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_DIV is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Signed Divide
	private int Execute_SDIV(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("SDIV requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operandRead.Cycles;

		uint result = 0;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
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
					cycles += BitHelper.NumberOfOnes((uint)dividend); // Roughly number of adds/subs needed

					// Negate result decision
					cycles++;

					result = (byte)(dividend / divisor); // Quotient in lower half
					result |= (uint)(byte)(dividend % divisor) << 8; // Remainder in upper half
				}

				break;
			}

			case Constants.DataSize.DWord:
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
					cycles += BitHelper.NumberOfOnes((uint)dividend); // Roughly number of adds/subs needed

					// Negate result decision
					cycles++;

					result = (ushort)(dividend / divisor); // Quotient in lower half
					result |= (uint)(ushort)(dividend % divisor) << 16; // Remainder in upper half
				}

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_SDIV is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// AND
	private int Execute_AND(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("AND requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operand1Read.Value;
				byte b = (byte)operand2Read.Value;

				result = (byte)(a & b);

				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operand1Read.Value;
				ushort b = (ushort)operand2Read.Value;

				result = (ushort)(a & b);

				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
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
				throw new EmulatorFaultException(
					$"Execute_AND is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// OR
	private int Execute_OR(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("OR requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operand1Read.Value;
				byte b = (byte)operand2Read.Value;

				result = (byte)(a | b);

				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operand1Read.Value;
				ushort b = (ushort)operand2Read.Value;

				result = (ushort)(a | b);

				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
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
				throw new EmulatorFaultException(
					$"Execute_OR is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// XOR
	private int Execute_XOR(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("XOR requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operand1Read.Value;
				byte b = (byte)operand2Read.Value;

				result = (byte)(a ^ b);

				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operand1Read.Value;
				ushort b = (ushort)operand2Read.Value;

				result = (ushort)(a ^ b);

				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
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
				throw new EmulatorFaultException(
					$"Execute_XOR is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// TST
	private int Execute_TST(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("TST requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		cycles += Internal_TST(operand1Read.Value, operand2Read.Value, operation.DataSize);

		return cycles;
	}

	// CPL
	private int Execute_CPL(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("CPL requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)operand1Read.Value;

				result = (byte)(a ^ 0xFF);

				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				ushort a = (ushort)operand1Read.Value;

				result = (ushort)(a ^ 0xFFFF);

				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				uint a = operand1Read.Value;

				result = a ^ 0xFFFFFFFF;

				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_CPL is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Test bit
	private int Execute_BIT(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("BIT requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint bit = operand2Read.Value;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_BIT is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = (((int)operand1Read.Value >> (int)bit) & 1) == 0;
		UpdateALUFlags(flagState);

		return cycles;
	}

	// Set bit
	private int Execute_SET(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("SET requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint bit = operand2Read.Value;
		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_SET is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		uint result = operand1Read.Value;
		result |= (uint)(1 << (int)bit);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Reset bit
	private int Execute_RES(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("RES requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		uint bit = operand2Read.Value;
		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_RES is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		uint result = operand1Read.Value;
		result &= ~(uint)(1 << (int)bit);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Rotate left circular
	private int Execute_RLC(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("RLC requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		ALUFlagState flagState = GetALUFlags();

		uint bit = operand2Read.Value;
		uint result;
		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)operand1Read.Value;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit7 = (byte)(a & 0x80);
					a = (byte)((a << 1) | (bit7 >> 7));
					flagState.Carry = bit7 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					ushort bit15 = (ushort)(a & 0x8000);
					a = (ushort)((a << 1) | (bit15 >> 15));
					flagState.Carry = bit15 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				uint a = operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					uint bit31 = a & 0x80000000;
					a = (a << 1) | (bit31 >> 31);
					flagState.Carry = bit31 != 0;
					cycles += Config.DWordALUCost;
				}

				result = a;
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_RLC is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Rotate right circular
	private int Execute_RRC(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("RRC requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		ALUFlagState flagState = GetALUFlags();

		uint bit = operand2Read.Value;
		uint result;
		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)operand1Read.Value;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit0 = (byte)(a & 1);
					a = (byte)((a >> 1) | (bit0 << 7));
					flagState.Carry = bit0 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					ushort bit0 = (ushort)(a & 1);
					a = (ushort)((a >> 1) | (bit0 << 15));
					flagState.Carry = bit0 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				uint a = operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					uint bit0 = a & 1;
					a = (a >> 1) | (bit0 << 31);
					flagState.Carry = bit0 != 0;
					cycles += Config.DWordALUCost;
				}

				result = a;
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_RRC is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Rotate left
	private int Execute_RL(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("RL requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		ALUFlagState flagState = GetALUFlags();

		uint bit = operand2Read.Value;
		uint result;
		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)operand1Read.Value;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit7 = (byte)(a & 0x80);
					a = (byte)(a << 1);
					a |= (byte)(flagState.Carry ? 1 : 0);
					flagState.Carry = bit7 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					ushort bit15 = (ushort)(a & 0x8000);
					a = (ushort)(a << 1);
					a |= (ushort)(flagState.Carry ? 1 : 0);
					flagState.Carry = bit15 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				uint a = operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					uint bit31 = a & 0x80000000;
					a = a << 1;
					a |= (uint)(flagState.Carry ? 1 : 0);
					flagState.Carry = bit31 != 0;
					cycles += Config.DWordALUCost;
				}

				result = a;
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_SLA is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Rotate right
	private int Execute_RR(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("RR requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		ALUFlagState flagState = GetALUFlags();

		uint bit = operand2Read.Value;
		uint result;
		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)operand1Read.Value;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit0 = (byte)(a & 1);
					a = (byte)((a >> 1) | ((flagState.Carry ? 1 : 0) << 7));
					flagState.Carry = bit0 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					ushort bit0 = (ushort)(a & 1);
					a = (ushort)((a >> 1) | ((flagState.Carry ? 1 : 0) << 15));
					flagState.Carry = bit0 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				uint a = operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					uint bit0 = a & 1;
					a = (a >> 1) | ((uint)(flagState.Carry ? 1 : 0) << 31);
					flagState.Carry = bit0 != 0;
					cycles += Config.DWordALUCost;
				}

				result = a;
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_RR is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Shift left arithmetic
	private int Execute_SLA(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("SLA requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		ALUFlagState flagState = GetALUFlags();

		uint bit = operand2Read.Value;
		uint result;
		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)operand1Read.Value;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit7 = (byte)(a & 0x80);
					a = (byte)(a << 1);
					flagState.Carry = bit7 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					ushort bit15 = (ushort)(a & 0x8000);
					a = (ushort)(a << 1);
					flagState.Carry = bit15 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				uint a = operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					uint bit31 = a & 0x80000000;
					a = a << 1;
					flagState.Carry = bit31 != 0;
					cycles += Config.DWordALUCost;
				}

				result = a;
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_SLA is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Shift right arithmetic
	private int Execute_SRA(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("SRA requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		ALUFlagState flagState = GetALUFlags();

		uint bit = operand2Read.Value;
		uint result;
		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)operand1Read.Value;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit0 = (byte)(a & 1);
					a = (byte)(a >> 1);
					flagState.Carry = bit0 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					ushort bit0 = (ushort)(a & 1);
					a = (ushort)(a >> 1);
					flagState.Carry = bit0 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				uint a = operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					uint bit0 = a & 1;
					a >>= 1;
					flagState.Carry = bit0 != 0;
					cycles += Config.DWordALUCost;
				}

				result = a;
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_SRA is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Shift right logical
	private int Execute_SRL(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is null)
		{
			throw new EmulatorFaultException("SRL requires two operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operand1Read = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
		cycles += operand1Read.Cycles;

		MemoryResult operand2Read = GetOperandValue(operation.Operand2!.Value, operation.DataSize);
		cycles += operand2Read.Cycles;

		ALUFlagState flagState = GetALUFlags();

		uint bit = operand2Read.Value;
		uint result;
		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)operand1Read.Value;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit0 = (byte)(a & 1);
					a = (byte)(a >>> 1);
					flagState.Carry = bit0 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					ushort bit0 = (ushort)(a & 1);
					a = (ushort)(a >>> 1);
					flagState.Carry = bit0 != 0;
					cycles++;
				}

				result = a;
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				bit &= 0b11111;
				uint a = operand1Read.Value;

				for (int i = 0; i < bit; i++)
				{
					uint bit0 = a & 1;
					a >>>= 1;
					flagState.Carry = bit0 != 0;
					cycles += Config.DWordALUCost;
				}

				result = a;
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException(
					$"Execute_SRL is not implemented for operand size {operation.DataSize}"
				);
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		MemoryResult operandWrite = WritebackOperand(operation.Operand1!.Value, operation.DataSize, result);
		cycles += operandWrite.Cycles;

		return cycles;
	}

	// Rotate Left Decimal
	private int Execute_RLD(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("RLD requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord);
		MemoryResult readHL = ReadMemory(hl, Constants.DataSize.Byte);
		cycles += readHL.Cycles;

		byte a = (byte)Registers.GetRegister(Constants.RegisterTargets.A, Constants.DataSize.Byte);
		byte mem = (byte)readHL.Value;

		byte aLow = (byte)(a & 0x0F);
		byte memHigh = (byte)((mem >> 4) & 0x0F);
		byte memLow = (byte)(mem & 0x0F);

		byte newA = (byte)((a & 0xF0) | memHigh);
		byte newMem = (byte)((memLow << 4) | aLow);

		MemoryResult writeHL = WriteMemory(hl, Constants.DataSize.Byte, newMem);
		cycles += writeHL.Cycles;

		ALUFlagState flagState = GetALUFlags();
		flagState.Sign = (newA & 0x80) != 0;
		flagState.Zero = newA == 0;
		flagState.ParityOverflow = BitHelper.IsParityEven(newA);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		UpdateALUFlags(flagState);

		Registers.SetRegister(Constants.RegisterTargets.A, Constants.DataSize.Byte, newA);

		cycles += 2; // Extra cycles for nibble manipulation
		return cycles;
	}

	// Rotate Right Decimal
	private int Execute_RRD(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("RRD requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord);
		MemoryResult readHL = ReadMemory(hl, Constants.DataSize.Byte);
		cycles += readHL.Cycles;

		byte a = (byte)Registers.GetRegister(Constants.RegisterTargets.A, Constants.DataSize.Byte);
		byte mem = (byte)readHL.Value;

		byte aLow = (byte)(a & 0x0F);
		byte memHigh = (byte)((mem >> 4) & 0x0F);
		byte memLow = (byte)(mem & 0x0F);

		byte newA = (byte)((a & 0xF0) | memLow);
		byte newMem = (byte)((aLow << 4) | memHigh);

		MemoryResult writeHL = WriteMemory(hl, Constants.DataSize.Byte, newMem);
		cycles += writeHL.Cycles;

		ALUFlagState flagState = GetALUFlags();
		flagState.Sign = (newA & 0x80) != 0;
		flagState.Zero = newA == 0;
		flagState.ParityOverflow = BitHelper.IsParityEven(newA);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		UpdateALUFlags(flagState);

		Registers.SetRegister(Constants.RegisterTargets.A, Constants.DataSize.Byte, newA);

		cycles += 2; // Extra cycles for nibble manipulation
		return cycles;
	}

	// No operation
	private int Execute_NOP(in DecodedOperation operation)
	{
		return operation.FetchCycles + 1;
	}

	// Jump
	private int Execute_JP(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("JP requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		if (IsConditionTrue(operation.Condition))
		{
			MemoryResult addressRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
			cycles += addressRead.Cycles;

			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, addressRead.Value);
		}

		return cycles;
	}

	// Jump Relative Short
	private int Execute_JR_s8(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("JR_s8 requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		if (IsConditionTrue(operation.Condition))
		{
			MemoryResult addressRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
			cycles += addressRead.Cycles;

			int displacement = (int)BitHelper.SignExtend(addressRead.Value, 8);

			int pc = (int)Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			pc += displacement;
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, (uint)pc);

			cycles += 2;
		}

		return cycles;
	}

	// Jump Relative
	private int Execute_JR(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("JR requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		if (IsConditionTrue(operation.Condition))
		{
			MemoryResult addressRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
			cycles += addressRead.Cycles;

			int displacement = (int)BitHelper.SignExtend(addressRead.Value, 16);

			int pc = (int)Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			pc += displacement;
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, (uint)pc);

			cycles += 2;
		}

		return cycles;
	}

	// Call
	private int Execute_CALL(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("CALL requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		if (IsConditionTrue(operation.Condition))
		{
			uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			cycles += Internal_Push(pc);

			MemoryResult addressRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
			cycles += addressRead.Cycles;

			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, addressRead.Value);
		}

		return cycles;
	}

	// Call Relative Short
	private int Execute_CALLR_s8(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("CALLR_s8 requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		if (IsConditionTrue(operation.Condition))
		{
			uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			cycles += Internal_Push(pc);

			MemoryResult addressRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
			cycles += addressRead.Cycles;

			int displacement = (int)BitHelper.SignExtend(addressRead.Value, 8);
			pc = (uint)((int)pc + displacement);
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, pc);

			cycles += 2;
		}

		return cycles;
	}

	// Call Relative
	private int Execute_CALLR(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("CALLR requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		if (IsConditionTrue(operation.Condition))
		{
			uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			cycles += Internal_Push(pc);

			MemoryResult addressRead = GetOperandValue(operation.Operand1!.Value, operation.DataSize);
			cycles += addressRead.Cycles;

			int displacement = (int)BitHelper.SignExtend(addressRead.Value, 16);
			pc = (uint)((int)pc + displacement);
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, pc);

			cycles += 2;
		}

		return cycles;
	}

	// Return
	private int Execute_RET(in DecodedOperation operation)
	{
		if (operation.Operand2 is not null)
		{
			// RET is weird. Has a register select field that isn't used because
			// it's in the B-Type encoding group
			throw new EmulatorFaultException("RET requires zero or one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		if (IsConditionTrue(operation.Condition))
		{
			MemoryResult popRead = Internal_Pop();
			cycles += popRead.Cycles;
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, popRead.Value);
		}

		return cycles;
	}

	// Return from Interrupt
	// Restores IFF1 from IFF2 and returns from interrupt service routine
	private int Execute_RETI(in DecodedOperation operation)
	{
		if (operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("RETI requires zero or one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		// Restore IFF1 from IFF2
		bool iff2 = Registers.GetFlag(Constants.FlagMasks.EnableInterruptsSave);
		Registers.SetFlag(Constants.FlagMasks.EnableInterrupts, iff2);

		MemoryResult popRead = Internal_Pop();
		cycles += popRead.Cycles;
		Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, popRead.Value);

		return cycles;
	}

	// Return from Non-Maskable Interrupt
	// Restores IFF1 from IFF2 and returns from NMI service routine
	private int Execute_RETN(in DecodedOperation operation)
	{
		if (operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("RETN requires zero or one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		// Restore IFF1 from IFF2 (saved on NMI entry)
		bool iff2 = Registers.GetFlag(Constants.FlagMasks.EnableInterruptsSave);
		Registers.SetFlag(Constants.FlagMasks.EnableInterrupts, iff2);

		MemoryResult popRead = Internal_Pop();
		cycles += popRead.Cycles;
		Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, popRead.Value);

		return cycles;
	}

	// Decrement B, Jump if Not Zero
	private int Execute_DJNZ(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("DJNZ requires one operand");
		}

		if (operation.Operand1!.Value.Immediate is null)
		{
			throw new EmulatorFaultException("DJNZ requires one immediate operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		ushort b = (ushort)Registers.GetRegister(Constants.RegisterTargets.B, Constants.DataSize.Word);
		b--;
		Registers.SetRegister(Constants.RegisterTargets.B, Constants.DataSize.Word, b);

		if (b != 0)
		{
			int pc = (int)Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			pc += (int)BitHelper.SignExtend(operation.Operand1!.Value.Immediate.Value, 8);
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, (uint)pc);
			cycles += 2;
		}

		return cycles;
	}

	// Jump if A Not Zero
	private int Execute_JANZ(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("JANZ requires one operand");
		}

		if (operation.Operand1!.Value.Immediate is null)
		{
			throw new EmulatorFaultException("JANZ requires one immediate operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		ushort a = (ushort)Registers.GetRegister(Constants.RegisterTargets.A, Constants.DataSize.Word);

		if (a != 0)
		{
			uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			pc += BitHelper.SignExtend(operation.Operand1!.Value.Immediate.Value, 8);
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, pc);
			cycles += 2;
		}

		return cycles;
	}

	// Jump if A is Zero
	private int Execute_JAZ(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("JAZ requires one operand");
		}

		if (operation.Operand1!.Value.Immediate is null)
		{
			throw new EmulatorFaultException("JAZ requires one immediate operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		ushort a = (ushort)Registers.GetRegister(Constants.RegisterTargets.A, Constants.DataSize.Word);

		if (a == 0)
		{
			uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			pc += BitHelper.SignExtend(operation.Operand1!.Value.Immediate.Value, 8);
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, pc);
			cycles += 2;
		}

		return cycles;
	}

	// Reset to Vector
	private int Execute_RST(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("RST requires one operand");
		}

		if (operation.Operand1!.Value.Immediate is null)
		{
			throw new EmulatorFaultException("RST requires one immediate operand");
		}

		return operation.FetchCycles + Internal_ServiceInterrupt((byte)operation.Operand1!.Value.Immediate);
	}

	// Set Carry Flag
	private int Execute_SCF(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("SCF requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		Registers.SetFlag(Constants.FlagMasks.Carry, true);

		return cycles;
	}

	// Complement Carry Flag
	private int Execute_CCF(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("CCF requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		bool carry = Registers.GetFlag(Constants.FlagMasks.Carry);
		Registers.SetFlag(Constants.FlagMasks.Carry, !carry);

		return cycles;
	}

	// Enable Interrupts
	private int Execute_EI(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("EI requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		_shouldEnableInterrupts = true;

		return cycles;
	}

	// Disable Interrupts
	private int Execute_DI(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("DI requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		Registers.SetFlag(Constants.FlagMasks.EnableInterrupts, false);
		Registers.SetFlag(Constants.FlagMasks.EnableInterruptsSave, false);

		return cycles;
	}

	// Set Interrupt Mode 1
	private int Execute_IM1(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("IM1 requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		_interruptMode = 1;

		return cycles;
	}

	// Set Interrupt Mode 2
	private int Execute_IM2(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("IM2 requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		_interruptMode = 2;

		return cycles;
	}

	// Halt - suspend execution until an interrupt occurs
	private int Execute_HALT(in DecodedOperation operation)
	{
		if (operation.Operand1 is not null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("HALT requires no operands");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		_isHalted = true;

		return cycles;
	}

	// Load Immediate into I register
	private int Execute_LD_I_NN(in DecodedOperation operation)
	{
		if (operation.Operand1 is null || operation.Operand2 is not null)
		{
			throw new EmulatorFaultException("LD_I_NN requires one operand");
		}

		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		MemoryResult operandRead = GetOperandValue(operation.Operand1!.Value, Constants.DataSize.DWord);
		cycles += operandRead.Cycles;

		Registers.SetRegister(Constants.RegisterTargets.I, Constants.DataSize.DWord, operandRead.Value);

		return cycles;
	}

	// Load A into R register
	private int Execute_LD_R_A(in DecodedOperation operation)
	{
		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		uint a = Registers.GetRegister(Constants.RegisterTargets.A, Constants.DataSize.Word);
		Registers.SetRegister(Constants.RegisterTargets.R, Constants.DataSize.Word, a);

		return cycles;
	}

	// Load R register into A
	private int Execute_LD_A_R(in DecodedOperation operation)
	{
		int cycles = operation.FetchCycles + Config.BaseInstructionCost;

		uint r = Registers.GetRegister(Constants.RegisterTargets.R, Constants.DataSize.Word);

		// Z80 sets flags and I don't know why. Don't like that.
		// ALUFlagState flagState = GetALUFlags();
		// flagState.Sign = (r & 0x80) != 0;
		// flagState.Zero = r == 0;
		// flagState.HalfCarry = false;
		// flagState.Subtract = false;
		// flagState.ParityOverflow = Registers.GetFlag(Constants.FlagMasks.EnableInterrupts);
		// UpdateALUFlags(flagState);

		Registers.SetRegister(Constants.RegisterTargets.A, Constants.DataSize.Word, r);

		return cycles;
	}

	private int Internal_Block_LD(Constants.DataSize size, bool increment)
	{
		int cycles = 0;

		uint bc = Registers.GetRegister(Constants.RegisterTargets.BC, Constants.DataSize.DWord);
		uint de = Registers.GetRegister(Constants.RegisterTargets.DE, Constants.DataSize.DWord);
		uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord);

		MemoryResult readHL = ReadMemory(hl, size);
		cycles += readHL.Cycles;
		MemoryResult writeDE = WriteMemory(de, size, readHL.Value);
		cycles += writeDE.Cycles;

		cycles++; // Extra cycle for increment/decrement logic

		uint adjustAmount = size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.DWord => 4,
			_ => throw new EmulatorFaultException($"Internal_Block_LD is not implemented for DataSize {size}"),
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

		Registers.SetRegister(Constants.RegisterTargets.BC, Constants.DataSize.DWord, bc);
		Registers.SetRegister(Constants.RegisterTargets.DE, Constants.DataSize.DWord, de);
		Registers.SetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord, hl);

		return cycles;
	}

	private int Internal_Block_CP(Constants.DataSize size, bool increment)
	{
		int cycles = 0;

		ushort a = (ushort)Registers.GetRegister(Constants.RegisterTargets.A, Constants.DataSize.Word);
		uint bc = Registers.GetRegister(Constants.RegisterTargets.BC, Constants.DataSize.DWord);
		uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord);

		MemoryResult readHL = ReadMemory(hl, size);
		cycles += readHL.Cycles;

		cycles += Internal_CP(a, readHL.Value, size);

		cycles++; // Extra cycle for increment/decrement logic

		uint adjustAmount = size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.DWord => 4,
			_ => throw new EmulatorFaultException($"Block_CP is not implemented for DataSize {size}"),
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

		Registers.SetRegister(Constants.RegisterTargets.BC, Constants.DataSize.DWord, bc);
		Registers.SetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord, hl);

		return cycles;
	}

	private int Internal_Block_TST(Constants.DataSize size, bool increment)
	{
		int cycles = 0;

		ushort a = (ushort)Registers.GetRegister(Constants.RegisterTargets.A, Constants.DataSize.Word);
		uint bc = Registers.GetRegister(Constants.RegisterTargets.BC, Constants.DataSize.DWord);
		uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord);

		MemoryResult readHL = ReadMemory(hl, size);
		cycles += readHL.Cycles;

		cycles += Internal_TST(a, readHL.Value, size);

		cycles++; // Extra cycle for increment/decrement logic

		uint adjustAmount = size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.DWord => 4,
			_ => throw new EmulatorFaultException($"Block_TST is not implemented for DataSize {size}"),
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

		Registers.SetRegister(Constants.RegisterTargets.BC, Constants.DataSize.DWord, bc);
		Registers.SetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord, hl);

		return cycles;
	}

	private int Internal_Block_IN(Constants.DataSize size, bool increment)
	{
		int cycles = 0;

		uint b = Registers.GetRegister(Constants.RegisterTargets.B, Constants.DataSize.Word);
		uint c = Registers.GetRegister(Constants.RegisterTargets.BC, Constants.DataSize.Word);
		uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord);

		MemoryResult readIO = ReadMemory(c, size, true);
		cycles += readIO.Cycles;

		MemoryResult writeHL = WriteMemory(hl, size, readIO.Value);
		cycles += writeHL.Cycles;

		cycles++; // Extra cycle for increment/decrement logic

		uint adjustAmount = size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.DWord => 4,
			_ => throw new EmulatorFaultException($"Internal_Block_IN is not implemented for DataSize {size}"),
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
		b--;

		ALUFlagState flagState = GetALUFlags();
		flagState.Zero = b == 0;
		UpdateALUFlags(flagState);

		// Extra cycle for register writeback
		cycles++;

		Registers.SetRegister(Constants.RegisterTargets.B, Constants.DataSize.Word, b);
		Registers.SetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord, hl);

		return cycles;
	}

	private int Internal_Block_OUT(Constants.DataSize size, bool increment)
	{
		int cycles = 0;

		uint b = Registers.GetRegister(Constants.RegisterTargets.B, Constants.DataSize.Word);
		uint c = Registers.GetRegister(Constants.RegisterTargets.BC, Constants.DataSize.Word);
		uint hl = Registers.GetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord);

		MemoryResult readHL = ReadMemory(hl, size);
		cycles += readHL.Cycles;

		MemoryResult writeIO = WriteMemory(c, size, readHL.Value, true);
		cycles += writeIO.Cycles;

		cycles++; // Extra cycle for increment/decrement logic

		uint adjustAmount = size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.DWord => 4,
			_ => throw new EmulatorFaultException($"Internal_Block_OUT is not implemented for DataSize {size}"),
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
		b--;

		ALUFlagState flagState = GetALUFlags();
		flagState.Zero = b == 0;
		UpdateALUFlags(flagState);

		// Extra cycle for register writeback
		cycles++;

		Registers.SetRegister(Constants.RegisterTargets.B, Constants.DataSize.Word, b);
		Registers.SetRegister(Constants.RegisterTargets.HL, Constants.DataSize.DWord, hl);

		return cycles;
	}

	private int Internal_CP(uint a, uint b, Constants.DataSize size)
	{
		int cycles = 0;
		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (size)
		{
			case Constants.DataSize.Byte:
			{
				a = (byte)a;
				b = (byte)b;

				result = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap((byte)a, (byte)b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow((byte)a, (byte)b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry((byte)a, (byte)b);
				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				a = (ushort)a;
				b = (ushort)b;

				result = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap((ushort)a, (ushort)b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow((ushort)a, (ushort)b);
				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				result = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException($"Internal_CP is not implemented for operand size {size}");
			}
		}

		flagState.Subtract = true;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		return cycles;
	}

	private int Internal_TST(uint a, uint b, Constants.DataSize size)
	{
		int cycles = 0;
		uint result;
		ALUFlagState flagState = GetALUFlags();

		switch (size)
		{
			case Constants.DataSize.Byte:
			{
				a = (byte)a;
				b = (byte)b;

				result = (byte)(a & b);

				flagState.Sign = (result & 0x80) != 0;

				break;
			}

			case Constants.DataSize.Word:
			{
				a = (ushort)a;
				b = (ushort)b;

				result = (ushort)(a & b);

				flagState.Sign = (result & 0x8000) != 0;

				break;
			}

			case Constants.DataSize.DWord:
			{
				cycles += Config.DWordALUCost;

				result = a & b;

				flagState.Sign = (result & 0x80000000) != 0;

				break;
			}

			default:
			{
				throw new EmulatorFaultException($"Internal_TST is not implemented for operand size {size}");
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(result);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = result == 0;
		UpdateALUFlags(flagState);

		return cycles;
	}

	private int Internal_Block_Loop(in DecodedOperation operation)
	{
		// If PV == 1 (BC != 0), continue
		if (Registers.GetFlag(Constants.FlagMasks.ParityOverflow))
		{
			// Undo PC increment (so we can re-fetch)
			uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			pc -= (uint)operation.OpcodeLength;
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, pc);

			// Additional cycle to do the PC decrement
			return 1;
		}

		// Otherwise, done. Advance normally.
		return 0;
	}

	private int Internal_IO_Block_Loop(in DecodedOperation operation)
	{
		// If Z == 0 (B != 0), continue
		if (!Registers.GetFlag(Constants.FlagMasks.Zero))
		{
			// Undo PC increment (so we can re-fetch)
			uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
			pc -= (uint)operation.OpcodeLength;
			Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, pc);

			// Additional cycle to do the PC decrement
			return 1;
		}

		// Otherwise, done. Advance normally.
		return 0;
	}

	private int Internal_Push(uint value)
	{
		int cycles = 0;

		uint sp = Registers.GetRegister(Constants.RegisterTargets.SP, Constants.DataSize.DWord);

		sp -= 4;
		MemoryResult pushWrite = WriteMemory(sp, Constants.DataSize.DWord, value);
		cycles += pushWrite.Cycles;

		Registers.SetRegister(Constants.RegisterTargets.SP, Constants.DataSize.DWord, sp);
		cycles += 2;

		return cycles;
	}

	private MemoryResult Internal_Pop()
	{
		uint sp = Registers.GetRegister(Constants.RegisterTargets.SP, Constants.DataSize.DWord);

		MemoryResult popRead = ReadMemory(sp, Constants.DataSize.DWord);

		sp += 4;
		Registers.SetRegister(Constants.RegisterTargets.SP, Constants.DataSize.DWord, sp);
		popRead.Cycles += 2;

		return popRead;
	}

	private int Internal_ServiceInterrupt(byte interruptNumber)
	{
		int cycles = 0;

		// Wake from halt if necessary
		_isHalted = false;

		uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
		cycles += Internal_Push(pc);

		uint vectorAddress = Registers.GetRegister(Constants.RegisterTargets.I, Constants.DataSize.DWord) << 10;
		vectorAddress |= (uint)interruptNumber << 2;

		MemoryResult vectorReadResult = ReadMemory(vectorAddress, Constants.DataSize.DWord);

		cycles += vectorReadResult.Cycles;
		Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, vectorReadResult.Value);

		return cycles;
	}
}
