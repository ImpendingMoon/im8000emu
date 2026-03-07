using im8000emu.Emulator;

namespace im8000emu;

internal class Program
{
	private static void Main(string[] args)
	{
		if (!BitConverter.IsLittleEndian)
		{
			Console.WriteLine("This emulator does not support big-endian architectures.");
			return;
		}

		if (args.Length < 1)
		{
			Console.WriteLine("Usage: im8000emu <ROM file>");
			return;
		}

		string filePath = args[0].Trim('"').Trim();

		if (!File.Exists(filePath))
		{
			Console.WriteLine($"Could not find file \"{filePath}\"");
			return;
		}

		byte[] fileData = File.ReadAllBytes(filePath);
		var memoryBus = new MemoryBus(fileData, 0x10000);

		var ioBus = new MemoryBus();

		var cpu = new CPU(memoryBus, ioBus);

		cpu.Reset();

		for (int i = 0; i < 150; i++)
		{
			try
			{
				DecodedOperation operation = cpu.Decode();

				Console.WriteLine(
					$"Executing: [{BitConverter.ToString(operation.Opcode.ToArray())}] {operation.DisplayString} at address 0x{cpu.Registers.GetRegister(Constants.RegisterTargets.PC, Constants.OperandSize.DWord):X8}"
				);

				int cycles = cpu.Execute(operation);
				Console.WriteLine($"T-cycles taken: {cycles}");

				Console.WriteLine(cpu.Registers.GetFullDisplayString());
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Exception during execution: {ex.Message}");

				// Advance PC to avoid infinite loop.
				uint pc = cpu.Registers.GetRegister(Constants.RegisterTargets.PC, Constants.OperandSize.DWord);
				pc += 2;
				cpu.Registers.SetRegister(Constants.RegisterTargets.PC, Constants.OperandSize.DWord, pc);
			}

			Console.WriteLine();
		}
	}
}
