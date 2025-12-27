namespace im8000emu;

internal class Program
{
    static void Main(string[] args)
    {
        var memoryBus = new Emulator.MemoryBus();
        var ioBus = new Emulator.MemoryBus();

        var cpu = new Emulator.CPU(memoryBus, ioBus);

        // Example: Run a loop of only `LD A, A` instructions (opcode 0x00 0x00)
        for (int i = 0; i < 10; i++)
        {
            var operation = cpu.Decode();

            Console.WriteLine($"Executing: [{BitConverter.ToString(operation.Opcode.ToArray())}] {operation.DisplayString}");

            cpu.Execute(operation);
        }
    }
}
