namespace im8000emu;

internal class Program
{
    static void Main(string[] args)
    {
        var memoryBus = new Emulator.MemoryBus();
        var ioBus = new Emulator.MemoryBus();

        var cpu = new Emulator.CPU(memoryBus, ioBus);

        //cpu.Registers.SetRegisterDWord(Emulator.Constants.RegisterTargets.PC, 1);

        // Example: Run a loop of whatever is in memory
        for (int i = 0; i < 10; i++)
        {
            var operation = cpu.Decode();

            Console.WriteLine($"Executing: [{BitConverter.ToString(operation.Opcode.ToArray())}] {operation.DisplayString} at address 0x{cpu.Registers.GetRegisterDWord(Emulator.Constants.RegisterTargets.PC):X8}");

            int cycles = cpu.Execute(operation);
            Console.WriteLine($"T-cycles taken: {cycles}");
        }
    }
}
