namespace im8000emu.Emulator;

internal class DecodedOperation
{
    public Constants.Operation Operation { get; set; } = Constants.Operation.None;
    public Constants.OperandSize OperandSize { get; set; } = Constants.OperandSize.Byte;
    public Operand? Operand1 { get; set; } = null;
    public Operand? Operand2 { get; set; } = null;
    public Constants.Condition Condition { get; set; } = Constants.Condition.Unconditional;
    public uint BaseAddress { get; set; } = 0x0000_0000;
    public List<byte> Opcode { get; set; } = [];
    public string DisplayString { get; set; } = string.Empty;
}
