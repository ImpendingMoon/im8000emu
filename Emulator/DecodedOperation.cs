namespace im8000emu.Emulator;

internal class DecodedOperation
{
    public uint BaseAddress { get; set; }
    public byte[] Opcode { get; set; } = [];
    public string DisplayString { get; set; } = string.Empty;
}
