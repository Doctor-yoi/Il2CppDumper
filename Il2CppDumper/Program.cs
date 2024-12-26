using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Il2CppDumper
{
    class Program
    {
        private static Config config;

        [STAThread]
        static void Main(string[] args)
        {
            config = JsonSerializer.Deserialize<Config>(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + @"config.json"));
            string il2CppPath = null;
            string metadataPath = null;
            string outputDir = null;

            if (args.Length == 1)
            {
                if (args[0] == "-h" || args[0] == "--help" || args[0] == "/?" || args[0] == "/h")
                {
                    ShowHelp();
                    return;
                }
            }
            if (args.Length > 3)
            {
                ShowHelp();
                return;
            }
            if (args.Length > 1)
            {
                foreach (var arg in args)
                {
                    if (File.Exists(arg))
                    {
                        var file = File.ReadAllBytes(arg);
                        if (BitConverter.ToUInt32(file, 0) == 0xFAB11BAF)
                        {
                            metadataPath = arg;
                        }
                        else
                        {
                            il2CppPath = arg;
                        }
                    }
                    else if (Directory.Exists(arg))
                    {
                        outputDir = Path.GetFullPath(arg) + Path.DirectorySeparatorChar;
                    }
                }
            }
            outputDir ??= AppDomain.CurrentDomain.BaseDirectory;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (il2CppPath == null)
                {
                    var ofd = new OpenFileDialog
                    {
                        Filter = "Il2Cpp binary file|*.*"
                    };
                    if (ofd.ShowDialog())
                    {
                        il2CppPath = ofd.FileName;
                        ofd.Filter = "global-metadata|global-metadata.dat";
                        if (ofd.ShowDialog())
                        {
                            metadataPath = ofd.FileName;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }
            if (il2CppPath == null)
            {
                ShowHelp();
                return;
            }
            if (metadataPath == null)
            {
                Console.WriteLine(Resource1.Error_MetadataNotFound);
            }
            else
            {
                try
                {
                    if (Init(il2CppPath, metadataPath, out var metadata, out var il2Cpp))
                    {
                        Dump(metadata, il2Cpp, outputDir);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            if (config.RequireAnyKey)
            {
                Console.WriteLine(Resource1.Global_Exit_RequireKey);
                Console.ReadKey(true);
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine(Resource1.Global_Usage, AppDomain.CurrentDomain.FriendlyName);
        }

        private static bool Init(string il2cppPath, string metadataPath, out Metadata metadata, out Il2Cpp il2Cpp)
        {
            Console.WriteLine(Resource1.Global_Init_Metadata);
            var metadataBytes = File.ReadAllBytes(metadataPath);
            metadata = new Metadata(new MemoryStream(metadataBytes));
            Console.WriteLine(Resource1.Global_MetadataVersion, metadata.Version);

            Console.WriteLine(Resource1.Global_Init_Il2cpp);
            var il2CppBytes = File.ReadAllBytes(il2cppPath);
            var il2CppMagic = BitConverter.ToUInt32(il2CppBytes, 0);
            var il2CppMemory = new MemoryStream(il2CppBytes);
            switch (il2CppMagic)
            {
                default:
                    throw new NotSupportedException(Resource1.Error_Il2cppNotSupport);
                case 0x6D736100:
                    var web = new WebAssembly(il2CppMemory);
                    il2Cpp = web.CreateMemory();
                    break;
                case 0x304F534E:
                    var nso = new NSO(il2CppMemory);
                    il2Cpp = nso.UnCompress();
                    break;
                case 0x905A4D: //PE
                    il2Cpp = new PE(il2CppMemory);
                    break;
                case 0x464c457f: //ELF
                    if (il2CppBytes[4] == 2) //ELF64
                    {
                        il2Cpp = new Elf64(il2CppMemory);
                    }
                    else
                    {
                        il2Cpp = new Elf(il2CppMemory);
                    }
                    break;
                case 0xCAFEBABE: //FAT Mach-O
                case 0xBEBAFECA:
                    var machofat = new MachoFat(new MemoryStream(il2CppBytes));
                    Console.Write(Resource1.Global_Macho_SelectPlatform);
                    for (var i = 0; i < machofat.fats.Length; i++)
                    {
                        var fat = machofat.fats[i];
                        Console.Write(fat.magic == 0xFEEDFACF ? $"{i + 1}.64bit " : $"{i + 1}.32bit ");
                    }
                    Console.WriteLine();
                    var key = Console.ReadKey(true);
                    var index = int.Parse(key.KeyChar.ToString()) - 1;
                    var magic = machofat.fats[index % 2].magic;
                    il2CppBytes = machofat.GetMacho(index % 2);
                    il2CppMemory = new MemoryStream(il2CppBytes);
                    if (magic == 0xFEEDFACF)
                        goto case 0xFEEDFACF;
                    else
                        goto case 0xFEEDFACE;
                case 0xFEEDFACF: // 64bit Mach-O
                    il2Cpp = new Macho64(il2CppMemory);
                    break;
                case 0xFEEDFACE: // 32bit Mach-O
                    il2Cpp = new Macho(il2CppMemory);
                    break;
            }
            var version = config.ForceIl2CppVersion ? config.ForceVersion : metadata.Version;
            il2Cpp.SetProperties(version, metadata.metadataUsagesCount);
            Console.WriteLine(Resource1.Global_Il2cppVersion, il2Cpp.Version);
            if (config.ForceDump || il2Cpp.CheckDump())
            {
                if (il2Cpp is ElfBase elf)
                {
                    Console.WriteLine(Resource1.Global_Il2cpp_MayBeDumpWarning);
                    Console.WriteLine(Resource1.Global_Il2cpp_RequireDumpAddress);
                    var dumpAddr = Convert.ToUInt64(Console.ReadLine(), 16);
                    if (dumpAddr != 0)
                    {
                        il2Cpp.ImageBase = dumpAddr;
                        il2Cpp.IsDumped = true;
                        if (!config.NoRedirectedPointer)
                        {
                            elf.Reload();
                        }
                    }
                }
                else
                {
                    il2Cpp.IsDumped = true;
                }
            }

            Console.WriteLine(Resource1.Global_Searching);
            try
            {
                var flag = il2Cpp.PlusSearch(metadata.methodDefs.Count(x => x.methodIndex >= 0), metadata.typeDefs.Length, metadata.imageDefs.Length);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (!flag && il2Cpp is PE)
                    {
                        Console.WriteLine(Resource1.PE_CustomPELoader);
                        il2Cpp = PELoader.Load(il2cppPath);
                        il2Cpp.SetProperties(version, metadata.metadataUsagesCount);
                        flag = il2Cpp.PlusSearch(metadata.methodDefs.Count(x => x.methodIndex >= 0), metadata.typeDefs.Length, metadata.imageDefs.Length);
                    }
                }
                if (!flag)
                {
                    flag = il2Cpp.Search();
                }
                if (!flag)
                {
                    flag = il2Cpp.SymbolSearch();
                }
                if (!flag)
                {
                    Console.WriteLine(Resource1.Error_UseManualMode);
                    Console.Write(Resource1.Global_RequireCodeReg);
                    var codeRegistration = Convert.ToUInt64(Console.ReadLine(), 16);
                    Console.Write(Resource1.Global_RequireMetadataReg);
                    var metadataRegistration = Convert.ToUInt64(Console.ReadLine(), 16);
                    il2Cpp.Init(codeRegistration, metadataRegistration);
                }
                if (il2Cpp.Version >= 27 && il2Cpp.IsDumped)
                {
                    var typeDef = metadata.typeDefs[0];
                    var il2CppType = il2Cpp.types[typeDef.byvalTypeIndex];
                    metadata.ImageBase = il2CppType.data.typeHandle - metadata.header.typeDefinitionsOffset;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine(Resource1.Error_DefaultError);
                return false;
            }
            return true;
        }

        private static void Dump(Metadata metadata, Il2Cpp il2Cpp, string outputDir)
        {
            Console.WriteLine(Resource1.Global_Dumping);
            var executor = new Il2CppExecutor(metadata, il2Cpp);
            var decompiler = new Il2CppDecompiler(executor);
            decompiler.Decompile(config, outputDir);
            Console.WriteLine(Resource1.Global_Success);
            if (config.GenerateStruct)
            {
                Console.WriteLine(Resource1.StructGenerator_Start);
                var scriptGenerator = new StructGenerator(executor);
                scriptGenerator.WriteScript(outputDir);
                Console.WriteLine(Resource1.Global_Success);
                GC.Collect(2,GCCollectionMode.Forced); // 手动gc一下2代堆，能降大概1个g左右的内存吧
            }
            if (config.GenerateDummyDll)
            {
                Console.WriteLine(Resource1.DummyAssemblyExporter_Start);
                DummyAssemblyExporter.Export(executor, outputDir, config.DummyDllAddToken);
                Console.WriteLine(Resource1.Global_Success);
                GC.Collect(2,GCCollectionMode.Forced);
            }
            if (config.ExportProtocol)
            {
                Console.WriteLine(Resource1.ProtocolExport_Start);
                ProtocolExporter.Export(executor, outputDir);
                Console.WriteLine(Resource1.Global_Success);
                GC.Collect(2,GCCollectionMode.Forced);
            }
        }
    }
}
