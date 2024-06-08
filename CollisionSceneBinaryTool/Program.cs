using CollisionSceneBinaryTool;
using System;
using System.Reflection.PortableExecutable;

namespace CollisionSceneBinaryCLI
{
    internal sealed class Program
    {
        public static void Main(string[] args)
        {
            args = new string[] { "mri_05.csb.zst" };

            if (args.Length == 0 || args.Contains("-h"))
            {
                Console.WriteLine($"Usage:");
                Console.WriteLine($"    CollisionSceneBinaryCLI.exe file.csb (arguments)");
                Console.WriteLine($"    CollisionSceneBinaryCLI.exe file.dae (arguments)");

                Console.WriteLine($"Arguments:");
                Console.WriteLine($"-big (big endian, needed for color splash)");
                return;
            }

            bool is_big_endian = args.Contains("-big");

            foreach (string arg in args)
            {
                if (arg.EndsWith("csb.zst")) //compressed export
                {
                    Console.WriteLine($"Exporting compressed CSB file!");

                    var csb = new CsbFile(Zstd.Decompress(arg));
                    var output = arg.Replace("csb.zst", "dae");
                    CsbExporter.Export(csb, output);
                }
                else if (arg.EndsWith("csb")) //export
                {
                    Console.WriteLine($"Exporting CSB file!");

                    var csb = new CsbFile(File.OpenRead(arg));
                    var output = arg.Replace("csb", "dae");
                    CsbExporter.Export(csb, output);
                }
                else if (arg.EndsWith("dae")) //import and create
                {
                    Console.WriteLine($"Generating CSB and CTB files!");

                    var output_name = Path.GetFileNameWithoutExtension(arg);
                    var dir = Path.GetDirectoryName(arg);
                    CsbImporter.Import(arg, output_name, dir, is_big_endian);
                }
            }
        }
    }
}
