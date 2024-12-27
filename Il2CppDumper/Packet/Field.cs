#nullable enable
using System.IO;
using Mono.Cecil;

namespace Il2CppDumper.Packet;

public class Field
{
    public string Name { get; set; }
    public object? Value { get; set; }
    public TypeDefinition Type { get; set; }

    public Field(string name, object? value, TypeDefinition type)
    {
        Name = name;
        Value = value;
        Type = type;
    }
}