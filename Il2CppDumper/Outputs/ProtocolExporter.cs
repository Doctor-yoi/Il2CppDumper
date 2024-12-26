using System.IO;

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
            foreach (var assembly in dummy.Assemblies)
            {
                // TODO: Export logic
            }
        }
    }
}