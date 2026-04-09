namespace im8000emu.Emulator;

internal partial class CPU
{
	private readonly Executor[] _operationExecutors;

	public CPU(MemoryBus memoryBus, MemoryBus ioBus, InterruptBus interruptBus)
	{
		_memoryBus = memoryBus;
		_ioBus = ioBus;
		_interruptBus = interruptBus;

		// Methods defined in CPUExecute.cs, same order as Constants.Operation enum
		_operationExecutors = new Executor[(int)Constants.Operation.LD_A_R + 1];

		_operationExecutors[(int)Constants.Operation.None] = Execute_None;
		_operationExecutors[(int)Constants.Operation.Interrupt] = Execute_Interrupt;
		_operationExecutors[(int)Constants.Operation.NonMaskableInterrupt] = Execute_NonMaskableInterrupt;
		_operationExecutors[(int)Constants.Operation.HaltState] = Execute_HaltState;
		_operationExecutors[(int)Constants.Operation.LD] = Execute_LD;
		_operationExecutors[(int)Constants.Operation.LEA] = Execute_LEA;
		_operationExecutors[(int)Constants.Operation.EX] = Execute_EX;
		_operationExecutors[(int)Constants.Operation.EX_Alt] = Execute_EX_Alt;
		_operationExecutors[(int)Constants.Operation.EXX] = Execute_EXX;
		_operationExecutors[(int)Constants.Operation.EXI] = Execute_EXI;
		_operationExecutors[(int)Constants.Operation.EXH] = Execute_EXH;
		_operationExecutors[(int)Constants.Operation.PUSH] = Execute_PUSH;
		_operationExecutors[(int)Constants.Operation.POP] = Execute_POP;
		_operationExecutors[(int)Constants.Operation.IN_OUT] = Execute_IN_OUT;
		_operationExecutors[(int)Constants.Operation.LDI] = Execute_LDI;
		_operationExecutors[(int)Constants.Operation.LDIR] = Execute_LDIR;
		_operationExecutors[(int)Constants.Operation.LDD] = Execute_LDD;
		_operationExecutors[(int)Constants.Operation.LDDR] = Execute_LDDR;
		_operationExecutors[(int)Constants.Operation.CPI] = Execute_CPI;
		_operationExecutors[(int)Constants.Operation.CPIR] = Execute_CPIR;
		_operationExecutors[(int)Constants.Operation.CPD] = Execute_CPD;
		_operationExecutors[(int)Constants.Operation.CPDR] = Execute_CPDR;
		_operationExecutors[(int)Constants.Operation.TSI] = Execute_TSI;
		_operationExecutors[(int)Constants.Operation.TSIR] = Execute_TSIR;
		_operationExecutors[(int)Constants.Operation.TSD] = Execute_TSD;
		_operationExecutors[(int)Constants.Operation.TSDR] = Execute_TSDR;
		_operationExecutors[(int)Constants.Operation.INI] = Execute_INI;
		_operationExecutors[(int)Constants.Operation.INIR] = Execute_INIR;
		_operationExecutors[(int)Constants.Operation.IND] = Execute_IND;
		_operationExecutors[(int)Constants.Operation.INDR] = Execute_INDR;
		_operationExecutors[(int)Constants.Operation.OUTI] = Execute_OUTI;
		_operationExecutors[(int)Constants.Operation.OTIR] = Execute_OTIR;
		_operationExecutors[(int)Constants.Operation.OUTD] = Execute_OUTD;
		_operationExecutors[(int)Constants.Operation.OTDR] = Execute_OTDR;
		_operationExecutors[(int)Constants.Operation.ADD] = Execute_ADD;
		_operationExecutors[(int)Constants.Operation.ADC] = Execute_ADC;
		_operationExecutors[(int)Constants.Operation.SUB] = Execute_SUB;
		_operationExecutors[(int)Constants.Operation.SBC] = Execute_SBC;
		_operationExecutors[(int)Constants.Operation.CP] = Execute_CP;
		_operationExecutors[(int)Constants.Operation.INC] = Execute_INC;
		_operationExecutors[(int)Constants.Operation.DEC] = Execute_DEC;
		_operationExecutors[(int)Constants.Operation.DAA] = Execute_DAA;
		_operationExecutors[(int)Constants.Operation.NEG] = Execute_NEG;
		_operationExecutors[(int)Constants.Operation.EXT] = Execute_EXT;
		_operationExecutors[(int)Constants.Operation.MLT] = Execute_MLT;
		_operationExecutors[(int)Constants.Operation.DIV] = Execute_DIV;
		_operationExecutors[(int)Constants.Operation.SDIV] = Execute_SDIV;
		_operationExecutors[(int)Constants.Operation.AND] = Execute_AND;
		_operationExecutors[(int)Constants.Operation.OR] = Execute_OR;
		_operationExecutors[(int)Constants.Operation.XOR] = Execute_XOR;
		_operationExecutors[(int)Constants.Operation.TST] = Execute_TST;
		_operationExecutors[(int)Constants.Operation.CPL] = Execute_CPL;
		_operationExecutors[(int)Constants.Operation.BIT] = Execute_BIT;
		_operationExecutors[(int)Constants.Operation.SET] = Execute_SET;
		_operationExecutors[(int)Constants.Operation.RES] = Execute_RES;
		_operationExecutors[(int)Constants.Operation.RLC] = Execute_RLC;
		_operationExecutors[(int)Constants.Operation.RRC] = Execute_RRC;
		_operationExecutors[(int)Constants.Operation.RL] = Execute_RL;
		_operationExecutors[(int)Constants.Operation.RR] = Execute_RR;
		_operationExecutors[(int)Constants.Operation.SLA] = Execute_SLA;
		_operationExecutors[(int)Constants.Operation.SRA] = Execute_SRA;
		_operationExecutors[(int)Constants.Operation.SRL] = Execute_SRL;
		_operationExecutors[(int)Constants.Operation.RLD] = Execute_RLD;
		_operationExecutors[(int)Constants.Operation.RRD] = Execute_RRD;
		_operationExecutors[(int)Constants.Operation.NOP] = Execute_NOP;
		_operationExecutors[(int)Constants.Operation.JP] = Execute_JP;
		_operationExecutors[(int)Constants.Operation.JR_s8] = Execute_JR_s8;
		_operationExecutors[(int)Constants.Operation.JR] = Execute_JR;
		_operationExecutors[(int)Constants.Operation.CALL] = Execute_CALL;
		_operationExecutors[(int)Constants.Operation.CALLR_s8] = Execute_CALLR_s8;
		_operationExecutors[(int)Constants.Operation.CALLR] = Execute_CALLR;
		_operationExecutors[(int)Constants.Operation.RET] = Execute_RET;
		_operationExecutors[(int)Constants.Operation.RETI] = Execute_RETI;
		_operationExecutors[(int)Constants.Operation.RETN] = Execute_RETN;
		_operationExecutors[(int)Constants.Operation.DJNZ] = Execute_DJNZ;
		_operationExecutors[(int)Constants.Operation.JANZ] = Execute_JANZ;
		_operationExecutors[(int)Constants.Operation.JAZ] = Execute_JAZ;
		_operationExecutors[(int)Constants.Operation.RST] = Execute_RST;
		_operationExecutors[(int)Constants.Operation.CCF] = Execute_CCF;
		_operationExecutors[(int)Constants.Operation.SCF] = Execute_SCF;
		_operationExecutors[(int)Constants.Operation.EI] = Execute_EI;
		_operationExecutors[(int)Constants.Operation.DI] = Execute_DI;
		_operationExecutors[(int)Constants.Operation.IM1] = Execute_IM1;
		_operationExecutors[(int)Constants.Operation.IM2] = Execute_IM2;
		_operationExecutors[(int)Constants.Operation.HALT] = Execute_HALT;
		_operationExecutors[(int)Constants.Operation.LD_I_NN] = Execute_LD_I_NN;
		_operationExecutors[(int)Constants.Operation.LD_R_A] = Execute_LD_R_A;
		_operationExecutors[(int)Constants.Operation.LD_A_R] = Execute_LD_A_R;
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
		if (_interruptBus.HasPendingNmi)
		{
			return new DecodedOperation
			{
				Operation = Constants.Operation.NonMaskableInterrupt,
			};
		}

		if (_interruptBus.HasPendingInterrupt && Registers.GetFlag(Constants.FlagMasks.EnableInterrupts))
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

		int cycles = _operationExecutors[(int)instruction.Operation](in instruction);
		return cycles;
	}

	private delegate int Executor(in DecodedOperation instruction);
}
