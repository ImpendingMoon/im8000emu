namespace im8000emu.Emulator;

internal class DecodedOperation
{
    public uint BaseAddress { get; set; }
    public byte[] Opcode { get; set; } = [];
    public string DisplayString { get; set; } = string.Empty;
    public Operand? Operand1 { get; set; }
    public Operand? Operand2 { get; set; }
    public int OperandSize { get; set; }
}
