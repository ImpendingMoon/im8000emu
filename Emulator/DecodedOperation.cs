namespace im8000emu.Emulator;

internal struct DecodedOperation
{
	public Constants.Operation Operation;
	public Constants.DataSize DataSize;
	public Operand? Operand1;
	public Operand? Operand2;
	public Constants.Condition Condition;
	public uint BaseAddress;
	public int OpcodeLength;
	public int FetchCycles;

	public void Reset()
	{
		Operation = Constants.Operation.None;
		DataSize = Constants.DataSize.Byte;
		Operand1 = null;
		Operand2 = null;
		Condition = Constants.Condition.Unconditional;
		BaseAddress = 0;
		OpcodeLength = 0;
		FetchCycles = 0;
	}
}
