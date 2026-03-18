namespace im8000emu;

internal static class Config
{
	// The cost to execute an instruction after fetching
	public static int BaseInstructionCost => 1;
	// The cost to read/write from memory once
	public static int BusCycleCost => 3;
	// The cost to read/write from an IO device once
	public static int IOCycleCost => 4;
	// The cost to ripple-carry DWord operands through the ALU twice
	public static int DWordALUCost => 3;
	// Throw exceptions on illegal emulated system states
	public static bool EnableStrictMode => true;
	public static int CpuSpeedHz => 4_000_000;
	public static int TargetFramerate => 60;
	public static uint MemorySizeKiB => 0x4000;
	public static uint VideoMemorySizeKiB => 0x1000;
}
