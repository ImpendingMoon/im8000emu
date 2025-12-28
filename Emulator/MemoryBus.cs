namespace im8000emu.Emulator
{
    internal class MemoryBus
    {
        public MemoryBus()
        {
        }

        public List<MemoryBusDeviceMapping> Devices { get; } = [];

        public byte ReadByte(uint address)
        {
            foreach (var mapping in Devices)
            {
                if (mapping.ContainsAddress(address))
                {
                    return mapping.Device.ReadByte(address - mapping.StartAddress);
                }
            }
            return 0;
        }

        public void WriteByte(uint address, byte value)
        {
            foreach (var mapping in Devices)
            {
                if (mapping.ContainsAddress(address) && !mapping.ReadOnly)
                {
                    mapping.Device.WriteByte(address - mapping.StartAddress, value);
                    return;
                }
            }
        }

        public Span<byte> ReadByteArray(uint address, int length)
        {
            for (int i = 0; i < length; i++)
            {
                ReadByte(address + (uint)i);
            }
            return new byte[length];
        }

        public void WriteByteArray(uint address, Span<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                WriteByte(address + (uint)i, data[i]);
            }
        }

        public void AttachDevice(MemoryBusDeviceMapping deviceMapping)
        {
            Devices.Add(deviceMapping);
        }
    }
}
