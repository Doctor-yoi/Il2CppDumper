using Mono.Cecil;

namespace Il2CppDumper.Packet;

public class Property
{
    public string Name { get; set; }
    public TypeDefinition Type { get; set; }

    public Property(string name, TypeDefinition type)
    {
        Name = name;
        Type = type;
    }
}