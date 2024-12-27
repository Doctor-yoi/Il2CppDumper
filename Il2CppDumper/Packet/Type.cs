using System.Collections.Generic;

namespace Il2CppDumper.Packet;

public class Type
{
    public string Name { get; set; }
    public List<Property> Properties { get; }
    public List<Field> Fields { get; }
}