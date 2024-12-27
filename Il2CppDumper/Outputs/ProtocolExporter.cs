using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace Il2CppDumper
{
    public static class ProtocolExporter
    {
        private static List<TypeDefinition> UnexportedTypes = new();
        private static List<string> Exported = new();
        public static void Export(Il2CppExecutor il2CppExecutor, string outputDir)
        {
            Directory.SetCurrentDirectory(outputDir);
            if (Directory.Exists("Protocol"))
                Directory.Delete("Protocol", true);
            Directory.CreateDirectory("Protocol");
            Directory.SetCurrentDirectory("Protocol");
            var dummy = new DummyAssemblyGenerator(il2CppExecutor, true);
            var targetAssembly = dummy.Assemblies.FirstOrDefault(definition => definition.MainModule.Name == "BlueArchive.dll");
            if (targetAssembly == null)
            {
                Console.WriteLine(Resource1.Error_ProtocolExporter_TargetAssemblyNotFound);
                return;
            }
            var networkProtocolClassTypes = targetAssembly.MainModule.Types.ToList().FindAll(type => type.Namespace == "MX.NetworkProtocol" && type.BaseType!= null &&
                                                                                                     type.BaseType.FullName is "MX.NetworkProtocol.RequestPacket" or "MX.NetworkProtocol.ResponsePacket");
            var networkProtocolEnumTypes = targetAssembly.MainModule.Types.ToList().FindAll(type => type.Namespace == "MX.NetworkProtocol" && type.IsEnum);
            // export packets
            // find unexported types
            var fs = File.Open("packets.txt", FileMode.OpenOrCreate);
            foreach (var type in networkProtocolClassTypes)
            {
                fs.Write(Encoding.UTF8.GetBytes($"Packet Name: {type.Name}\n"));
                fs.Write(Encoding.UTF8.GetBytes("Members:\n"));
                foreach (var property in type.Properties)
                {
                    fs.Write(Encoding.UTF8.GetBytes($"  Name: {property.Name} >> Type: {parseTypeName(property.PropertyType)}\n"));
                    addUnexportedTypes(property.PropertyType);
                }
            }
            fs.Close();
            
            // export enums
            fs = File.Open("enums.txt", FileMode.OpenOrCreate);
            foreach (var type in networkProtocolEnumTypes)
            {
                fs.Write(Encoding.UTF8.GetBytes($"Enum Name: {type.Name}\n"));
                foreach (var field in type.Fields.Where(field => field.Name != "value__"))
                {
                    fs.Write(Encoding.UTF8.GetBytes($"  Name: {field.Name} >> Value: {(int)field.Constant}\n"));
                }
            }
            fs.Close();
            
            // export unexported types
            fs = File.Open("types.txt", FileMode.OpenOrCreate);
            while (UnexportedTypes.Count != 0)
            {
                for (var a = 0; a < UnexportedTypes.Count; a++)
                {
                    var type = UnexportedTypes[a];
                    Exported.Add(type.FullName);
                    
                    fs.Write(Encoding.UTF8.GetBytes($"Packet Name: {type.Name}\n"));
                    fs.Write(Encoding.UTF8.GetBytes("Members:\n"));
                    foreach (var field in from field in type.Fields let skip = field.CustomAttributes.Any(attr => attr.AttributeType.Name == "CompilerGenerated") where !skip select field)
                    {
                        fs.Write(Encoding.UTF8.GetBytes($"  Name: {field.Name} >> Value: {field.Constant} >> Type: {parseTypeName(field.FieldType)}\n"));
                        if (field.FieldType.GetType().Name != "GenericInstanceType") addUnexportedTypes(field.FieldType);
                    }
                    foreach (var property in from property in type.Properties let skip = property.CustomAttributes.Any(attr => attr.AttributeType.Name == "CompilerGenerated") where !skip select property)
                    {
                        fs.Write(Encoding.UTF8.GetBytes($"  Name: {property.Name} >> Type: {parseTypeName(property.PropertyType)}\n"));
                        addUnexportedTypes(property.PropertyType);
                    }

                    UnexportedTypes.Remove(type);
                }
            }
        }

        private static void addUnexportedTypes(TypeReference typeReference)
        {
            var typeDef = typeReference.Resolve();
            if (typeDef == null) return;
            if (typeDef.IsEnum && !Exported.Contains(typeDef.FullName))
            {
                Exported.Add(typeDef.FullName);
                using var fs = File.Open("enums.txt",FileMode.Append);
                fs.Write(Encoding.UTF8.GetBytes($"Enum Name: {typeDef.Name}\n"));
                foreach (var field in typeDef.Fields.Where(field => field.Name != "value__"))
                {
                    fs.Write(Encoding.UTF8.GetBytes($"  Name: {field.Name} >> Value: {field.Constant}\n"));
                }

                return;
            }
            if (!typeDef.IsClass || !typeDef.Namespace.Contains("MX.") || typeDef.Namespace == "MX.NetworkProtocol" || UnexportedTypes.Contains(typeDef) || Exported.Contains(typeDef.FullName)) return;
            UnexportedTypes.Add(typeDef);
        }
        private static string parseTypeName(TypeReference type)
        {
            if (type.GetType().Name != "GenericInstanceType") return type.Name;
            var p_genericArguments = type.GetType().GetProperty("GenericArguments");
            if (p_genericArguments == null) return "<!Error>";
            var genericArguments = (Collection<TypeReference>)p_genericArguments.GetValue(type);
            var output = type.Name[..^2] + "<";
            if (genericArguments == null) return output + "!Error>";
            var i = 1;
            foreach (var typeReference in genericArguments)
            {
                if (i != 1)
                {
                    output += ",";
                }

                output += parseTypeName(typeReference);
                addUnexportedTypes(typeReference);
                i++;
            }

            output += ">";
            return output;
        }
    }
}