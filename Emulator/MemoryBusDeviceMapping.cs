namespace im8000emu.Emulator;

internal class MemoryBusDeviceMapping
{
    public MemoryBusDeviceMapping(IMemoryDevice device, uint startAddress, uint endAddress, bool readOnly)
    {
        Device = device;
        StartAddress = startAddress;
        EndAddress = endAddress;
        ReadOnly = readOnly;
    }

    public IMemoryDevice Device { get; }
    public uint StartAddress { get; }
    public uint EndAddress { get; }
    public bool ReadOnly { get; }
    public bool ContainsAddress(uint address)
    {
        return address >= StartAddress && address <= EndAddress;
    }
}
