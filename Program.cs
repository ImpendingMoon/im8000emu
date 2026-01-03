namespace im8000emu;

internal class Program
{
    private static void Main(string[] args)
    {
        var memoryBus = new Emulator.MemoryBus();
        var ioBus = new Emulator.MemoryBus();

        var cpu = new Emulator.CPU(memoryBus, ioBus);

        //cpu.Reset();

        // Example: Run a loop of whatever is in memory
        for (int i = 0; i < 1000; i++)
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