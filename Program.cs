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


        var ioBus = new Emulator.MemoryBus();

        var cpu = new Emulator.CPU(memoryBus, ioBus);

        cpu.Reset();

        for (int i = 0; i < 150; i++)
        {
            try
            {
                Emulator.DecodedOperation operation = cpu.Decode();

                Console.WriteLine($"Executing: [{BitConverter.ToString(operation.Opcode.ToArray())}] {operation.DisplayString} at address 0x{cpu.Registers.GetRegister(Emulator.Constants.RegisterTargets.PC, Emulator.Constants.OperandSize.DWord):X8}");

                int cycles = cpu.Execute(operation);
                Console.WriteLine($"T-cycles taken: {cycles}");

                Console.WriteLine(cpu.Registers.GetFullDisplayString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during execution: {ex.Message}");

                // Advance PC to avoid infinite loop.
                uint pc = cpu.Registers.GetRegister(Emulator.Constants.RegisterTargets.PC, Emulator.Constants.OperandSize.DWord);
                pc += 2;
                cpu.Registers.SetRegister(Emulator.Constants.RegisterTargets.PC, Emulator.Constants.OperandSize.DWord, pc);
            }

            Console.WriteLine();
        }
    }
}
