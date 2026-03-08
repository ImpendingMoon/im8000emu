namespace im8000emu.Emulator;

internal struct Operand
{
	public Constants.RegisterTargets? Target; // Register target, if applicable
	public uint? Immediate; // Immediate value, if applicable
	public bool Indirect; // Fetch operand indirectly from Target or Immediate?
	public short? Displacement; // Signed offset for indirect access
}
