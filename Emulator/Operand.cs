namespace im8000emu.Emulator
{
    internal class Operand
    {
        public Constants.RegisterTargets? Target { get; set; } // Register target, if applicable
        public uint? Immediate { get; set; } // Immediate value, if applicable
        public bool Indirect { get; set; } // Fetch operand indirectly from Target or Immediate?
        public short? Displacement { get; set; } // Signed offset for indirect access
    }
}
