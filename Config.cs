using Microsoft.Extensions.Configuration;

namespace im8000emu;

internal static class Config
{
	private static readonly IConfiguration _configuration = new ConfigurationBuilder()
		.SetBasePath(AppContext.BaseDirectory)
		.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
		.Build();

	/// <summary>The cost to execute an instruction after fetching</summary>
	public static int BaseInstructionCost => _configuration.GetValue<int>("Emulator:BaseInstructionCost");
	/// <summary>The cost to read/write from memory once</summary>
	public static int BusCycleCost => _configuration.GetValue<int>("Emulator:BusCycleCost");
	/// <summary>The cost to read/write from an IO device once</summary>
	public static int IOCycleCost => _configuration.GetValue<int>("Emulator:IOCycleCost");
	/// <summary>The cost to move a DWord through the ALU</summary>
	public static int DWordALUCost => _configuration.GetValue<int>("Emulator:DWordALUCost");
	/// <summary>Throw exceptions on illegal emulated system states</summary>
	public static bool EnableStrictMode => _configuration.GetValue<bool>("Emulator:EnableStrictMode");
	/// <summary>Emulated CPU speed in Hz. Recommended 4,000,000 Hz, max is whatever your CPU can handle</summary>
	public static int CpuSpeedHz => _configuration.GetValue<int>("Emulator:CpuSpeedHz");
	/// <summary>Target framerate. Recommended 60 FPS. Lower values *might* help with slow emulation.</summary>
	public static int TargetFramerate => _configuration.GetValue<int>("Emulator:TargetFramerate");
	/// <summary>Main memory size in bytes. Min 16,384, max 12,582,912</summary>
	public static uint MemorySize => _configuration.GetValue<uint>("Emulator:MemorySize");
	/// <summary>VRAM size in bytes. Min 4,096, max 2,097,152</summary>
	public static uint VideoMemorySize => _configuration.GetValue<uint>("Emulator:VideoMemorySize");
}
