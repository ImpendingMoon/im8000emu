namespace im8000emu.Emulator.Devices;

public interface ISteppingDevice
{
	public static int CpuSpeedHz => Config.CpuSpeedHz;

	/// <summary>
	///		Steps the number of cycles the previous CPU instruction took
	/// </summary>
	/// <param name="cycles"></param>
	/// <returns>
	///		Any additional cycles taken by the device, such as a DMA
	///		controller taking control of the CPU bus.
	/// </returns>
	public int Step(int cycles);
}
