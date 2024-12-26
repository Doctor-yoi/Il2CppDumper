using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace Il2CppDumper
{
    public static class ProtocolExporter
    {
        public static void Export(Il2CppExecutor il2CppExecutor, string outputDir)
        {
            Directory.SetCurrentDirectory(outputDir);
            if (Directory.Exists("Protocol"))
                Directory.Delete("Protocol", true);
            Directory.CreateDirectory("Protocol");
            Directory.SetCurrentDirectory("Protocol");
            var dummy = new DummyAssemblyGenerator(il2CppExecutor, true);
            var targetAssembly = dummy.Assemblies.FirstOrDefault(definition =>
            {
                if (definition.MainModule.Name == "BlueArchive.dll") return true;
                return false;
            });
            if (targetAssembly == null)
            {
                Console.WriteLine(Resource1.Error_ProtocolExporter_TargetAssemblyNotFound);
                return;
            }
            var networkProtocolClassTypes = targetAssembly.MainModule.Types.ToList().FindAll(type =>
            {
                if (type.Namespace == "MX.NetworkProtocol" && type.BaseType!= null &&
                    (type.BaseType.FullName == "MX.NetworkProtocol.RequestPacket" ||
                     type.BaseType.FullName == "MX.NetworkProtocol.ResponsePacket" ||
                     type.BaseType.FullName == "MX.NetworkProtocol.BasePacket")) return true;
                return false;
            });
            var networkProtocolEnumTypes = targetAssembly.MainModule.Types.ToList().FindAll(type =>
            {
                if (type.Namespace == "MX.NetworkProtocol" && type.IsEnum) return true;
                return false;
            });
        }
    }
}