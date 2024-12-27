using System.Collections.Generic;

namespace Il2CppDumper.Packet;

public class Packet
{
    public Packet(string name, int packetId, List<Property> properties, PacketType packetType)
    {
        Name = name;
        PacketId = packetId;
        Properties = properties;
        PacketType = packetType;
    }

    public string Name { get; set; }
    public int PacketId { get; set; }
    public List<Property> Properties { get; }
    public PacketType PacketType { get; }
}