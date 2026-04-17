namespace im8000emu.Emulator;

internal partial class CPU
{
	public CPU(MemoryBus memoryBus, MemoryBus ioBus, InterruptBus interruptBus)
	{
		_memoryBus = memoryBus;
		_ioBus = ioBus;
		_interruptBus = interruptBus;
	}

	public Registers Registers { get; } = new();

	public void Reset()
	{
		Registers.ClearRegisters();
		// Read reset vector
		MemoryResult memoryResult = ReadMemory(0x00000000, Constants.DataSize.DWord);
		uint resetVector = memoryResult.Value;
		Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, resetVector);
	}

	/// <summary>
	///     Fetches and decodes the next operation, including interrupt servicing.
	/// </summary>
	public DecodedOperation Decode()
	{
		// If waiting for interrupts, handle them
		if (_interruptBus.IsNonMaskableInterruptPending())
		{
			return new DecodedOperation
			{
				Operation = Constants.Operation.NonMaskableInterrupt,
			};
		}

		if (_interruptBus.IsInterruptPending() && Registers.GetFlag(Constants.FlagMasks.EnableInterrupts))
		{
			return new DecodedOperation
			{
				Operation = Constants.Operation.Interrupt,
			};
		}

		if (_isHalted)
		{
			return new DecodedOperation
			{
				Operation = Constants.Operation.HaltState,
			};
		}

		// Else decode the operation at the current PC
		uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
		// Overload defined in CPUDecode.cs
		return Decode(pc);
	}

	/// <summary>
	///     Executes the decoded operation.
	/// </summary>
	/// <returns>Number of T-cycles taken.</returns>
	public int Execute(in DecodedOperation instruction)
	{
		// Advance PC
		uint pc = Registers.GetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord);
		pc += (uint)instruction.OpcodeLength;
		Registers.SetRegister(Constants.RegisterTargets.PC, Constants.DataSize.DWord, pc);

		// Advance Refresh
		ushort r = (ushort)Registers.GetRegister(Constants.RegisterTargets.R, Constants.DataSize.Word);
		r++;
		Registers.SetRegister(Constants.RegisterTargets.R, Constants.DataSize.Word, r);

		if (_shouldEnableInterrupts)
		{
			Registers.SetFlag(Constants.FlagMasks.EnableInterrupts, true);
			Registers.SetFlag(Constants.FlagMasks.EnableInterruptsSave, true);
			_shouldEnableInterrupts = false;
		}

		int cycles = instruction.Operation switch
		{
			Constants.Operation.None => Execute_None(in instruction),
			Constants.Operation.Interrupt => Execute_Interrupt(in instruction),
			Constants.Operation.NonMaskableInterrupt => Execute_NonMaskableInterrupt(in instruction),
			Constants.Operation.HaltState => Execute_HaltState(in instruction),
			Constants.Operation.LD => Execute_LD(in instruction),
			Constants.Operation.LEA => Execute_LEA(in instruction),
			Constants.Operation.EX => Execute_EX(in instruction),
			Constants.Operation.EX_Alt => Execute_EX_Alt(in instruction),
			Constants.Operation.EXX => Execute_EXX(in instruction),
			Constants.Operation.EXI => Execute_EXI(in instruction),
			Constants.Operation.EXH => Execute_EXH(in instruction),
			Constants.Operation.PUSH => Execute_PUSH(in instruction),
			Constants.Operation.POP => Execute_POP(in instruction),
			Constants.Operation.IN_OUT => Execute_IN_OUT(in instruction),
			Constants.Operation.LDI => Execute_LDI(in instruction),
			Constants.Operation.LDIR => Execute_LDIR(in instruction),
			Constants.Operation.LDD => Execute_LDD(in instruction),
			Constants.Operation.LDDR => Execute_LDDR(in instruction),
			Constants.Operation.CPI => Execute_CPI(in instruction),
			Constants.Operation.CPIR => Execute_CPIR(in instruction),
			Constants.Operation.CPD => Execute_CPD(in instruction),
			Constants.Operation.CPDR => Execute_CPDR(in instruction),
			Constants.Operation.TSI => Execute_TSI(in instruction),
			Constants.Operation.TSIR => Execute_TSIR(in instruction),
			Constants.Operation.TSD => Execute_TSD(in instruction),
			Constants.Operation.TSDR => Execute_TSDR(in instruction),
			Constants.Operation.INI => Execute_INI(in instruction),
			Constants.Operation.INIR => Execute_INIR(in instruction),
			Constants.Operation.IND => Execute_IND(in instruction),
			Constants.Operation.INDR => Execute_INDR(in instruction),
			Constants.Operation.OUTI => Execute_OUTI(in instruction),
			Constants.Operation.OTIR => Execute_OTIR(in instruction),
			Constants.Operation.OUTD => Execute_OUTD(in instruction),
			Constants.Operation.OTDR => Execute_OTDR(in instruction),
			Constants.Operation.ADD => Execute_ADD(in instruction),
			Constants.Operation.ADC => Execute_ADC(in instruction),
			Constants.Operation.SUB => Execute_SUB(in instruction),
			Constants.Operation.SBC => Execute_SBC(in instruction),
			Constants.Operation.CP => Execute_CP(in instruction),
			Constants.Operation.INC => Execute_INC(in instruction),
			Constants.Operation.DEC => Execute_DEC(in instruction),
			Constants.Operation.DAA => Execute_DAA(in instruction),
			Constants.Operation.NEG => Execute_NEG(in instruction),
			Constants.Operation.EXT => Execute_EXT(in instruction),
			Constants.Operation.MLT => Execute_MLT(in instruction),
			Constants.Operation.DIV => Execute_DIV(in instruction),
			Constants.Operation.SDIV => Execute_SDIV(in instruction),
			Constants.Operation.AND => Execute_AND(in instruction),
			Constants.Operation.OR => Execute_OR(in instruction),
			Constants.Operation.XOR => Execute_XOR(in instruction),
			Constants.Operation.TST => Execute_TST(in instruction),
			Constants.Operation.CPL => Execute_CPL(in instruction),
			Constants.Operation.BIT => Execute_BIT(in instruction),
			Constants.Operation.SET => Execute_SET(in instruction),
			Constants.Operation.RES => Execute_RES(in instruction),
			Constants.Operation.RLC => Execute_RLC(in instruction),
			Constants.Operation.RRC => Execute_RRC(in instruction),
			Constants.Operation.RL => Execute_RL(in instruction),
			Constants.Operation.RR => Execute_RR(in instruction),
			Constants.Operation.SLA => Execute_SLA(in instruction),
			Constants.Operation.SRA => Execute_SRA(in instruction),
			Constants.Operation.SRL => Execute_SRL(in instruction),
			Constants.Operation.RLD => Execute_RLD(in instruction),
			Constants.Operation.RRD => Execute_RRD(in instruction),
			Constants.Operation.NOP => Execute_NOP(in instruction),
			Constants.Operation.JP => Execute_JP(in instruction),
			Constants.Operation.JR_s8 => Execute_JR_s8(in instruction),
			Constants.Operation.JR => Execute_JR(in instruction),
			Constants.Operation.CALL => Execute_CALL(in instruction),
			Constants.Operation.CALLR_s8 => Execute_CALLR_s8(in instruction),
			Constants.Operation.CALLR => Execute_CALLR(in instruction),
			Constants.Operation.RET => Execute_RET(in instruction),
			Constants.Operation.RETI => Execute_RETI(in instruction),
			Constants.Operation.RETN => Execute_RETN(in instruction),
			Constants.Operation.DJNZ => Execute_DJNZ(in instruction),
			Constants.Operation.JANZ => Execute_JANZ(in instruction),
			Constants.Operation.JAZ => Execute_JAZ(in instruction),
			Constants.Operation.RST => Execute_RST(in instruction),
			Constants.Operation.CCF => Execute_CCF(in instruction),
			Constants.Operation.SCF => Execute_SCF(in instruction),
			Constants.Operation.EI => Execute_EI(in instruction),
			Constants.Operation.DI => Execute_DI(in instruction),
			Constants.Operation.IM1 => Execute_IM1(in instruction),
			Constants.Operation.IM2 => Execute_IM2(in instruction),
			Constants.Operation.HALT => Execute_HALT(in instruction),
			Constants.Operation.LD_I_NN => Execute_LD_I_NN(in instruction),
			Constants.Operation.LD_R_A => Execute_LD_R_A(in instruction),
			Constants.Operation.LD_A_R => Execute_LD_A_R(in instruction),
			_ => throw new NotImplementedException($"Unhandled operation: {instruction.Operation}"),
		};

		return cycles;
	}
}
