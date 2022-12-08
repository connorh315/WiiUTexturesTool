using ModLib;

namespace WiiUTexturesTool
{
    internal class Program
    {
        static string Version = "1.1";

        static void Main(string[] args)
        {
            //string input = @"A:\Dimensions\EXTRACT\LEVELS\STORY\11SCOOBYDOO\11SCOOBYDOOA\11SCOOBYDOOA_NXG_DESWIZZLED\Lightmap.3.dds";
            //using (ModFile file = ModFile.Open(input, true))
            //{
            //    file.Seek(12, SeekOrigin.Begin);
            //    int height = file.ReadInt();
            //    int width = file.ReadInt();
            //    file.Seek(8, SeekOrigin.Current);
            //    int mipmapCount = file.ReadInt();
            //    file.Seek(0x80, SeekOrigin.Begin);

            //    Pair[] pairs = new Pair[(int)Math.Ceiling(((float)(file.Length - file.Position)) / 16)];
            //    for (int i = 0; i < pairs.Length; i++)
            //    {
            //        pairs[i] = new Pair()
            //        {
            //            Part1 = file.ReadLong(),
            //            Part2 = file.ReadLong()
            //        };
            //    }

            //    Pair[] result = DXT1.Swizzle(pairs, width, height, mipmapCount);

            //    using (ModFile resultFile = ModFile.Create(@"A:\Dimensions\EXTRACT\LEVELS\STORY\11SCOOBYDOO\11SCOOBYDOOA\11SCOOBYDOOA_NXG_DESWIZZLED\Lightmap.3.1.dds"))
            //    {
            //        file.Seek(0, SeekOrigin.Begin);
            //        file.fileStream.SetLength(0x80);
            //        file.fileStream.CopyTo(resultFile.fileStream);

            //        for (int i = 0; i < result.Length; i++)
            //        {
            //            resultFile.WriteLong(result[i].Part1);
            //            resultFile.WriteLong(result[i].Part2);
            //        }
            //    }
            //}
            //return;
#if DEBUG
            //foreach (string file in Directory.EnumerateFiles(@"A:\Dimensions\EXTRACT\LEVELS\STORY\", "*.WIIU_TEXTURES", SearchOption.AllDirectories))
            //{
                ExtractSettings settings = new ExtractSettings()
                {
                    InputLocation = @"A:\Dimensions\EXTRACT\LEVELS\STORY\4DOCTORWHO\4DOCTORWHO_MATERIALS_NXG.WIIU_TEXTURES",
                    OutputLocation = @"A:\Dimensions\EXTRACT\LEVELS\STORY\4DOCTORWHO",
                    ShouldDeswizzle = true
                };
                Extractor.Extract(settings);
            //}
#else
            if (args.Length == 0)
            {
                Logger.Log(new LogSeg("WiiUTexturesTool - "), new LogSeg("Version {0}", ConsoleColor.DarkYellow, Version));
                Logger.Log(new LogSeg("Author: "), new LogSeg("Connor", ConsoleColor.Green));

                ShowOptions();
                Console.ReadKey();
                return;
            }

            string command = args[0].ToLower();
            if (command == "extract" || command == "e")
            {
                WUTExtractSettings settings = new WUTExtractSettings()
                {
                    ShouldDeswizzle = true
                };
                
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].Length == 0) continue;
                    
                    string arg = args[i];
                    if (ValidateArgument(arg, "input"))
                    {
                        settings.InputLocation = args[i + 1];
                        i++;
                    }
                    else if (ValidateArgument(arg, "output"))
                    {
                        settings.OutputLocation = args[i + 1];
                        i++;
                    }
                    else if (ValidateArgument(arg, "disable-deswizzle"))
                    {
                        settings.ShouldDeswizzle = false;
                    }
                    else if (arg.EndsWith("wiiu_textures"))
                    {
                        settings.InputLocation = args[i];
                    }
                    else
                    {
                        settings.OutputLocation = args[i];
                    }
                }

                Logger.Log("Extracting {0} into {1} with deswizzling {2}", settings.InputLocation, settings.OutputLocation, settings.ShouldDeswizzle ? "enabled" : "disabled");
                WiiUTextures.Extract(settings);
            }
            else if (command == "import" || command == "i")
            {
                Logger.Error("Not yet supported - Check if a newer version of WiiUTexturesTool is available!");
            }
            else
            {
                foreach (string arg in args)
                {
                    if (arg.ToLower().EndsWith("wiiu_textures"))
                    {
                        WiiUTextures.Extract(new WUTExtractSettings()
                        {
                            InputLocation = arg,
                            OutputLocation = GetOutputFromInput(arg),
                            ShouldDeswizzle = true
                        });
                    }
                }
            }
#endif
        }

        private static bool ValidateArgument(string arg, string option)
        {
            return (arg == option) || (arg == "-" + option[0]) || (arg == "--" + option[0]) || (arg == "-" + option) ||
                   (arg == "--" + option);
        }

        private static string GetOutputFromInput(string inputLocation)
        {
            return (Path.IsPathFullyQualified(inputLocation) ? Path.GetDirectoryName(inputLocation) : Directory.GetCurrentDirectory()) + "\\" + Path.GetFileNameWithoutExtension(inputLocation);
        }

        private static void ShowOptions()
        {
            Console.WriteLine();
            Logger.Log("What would you like to do?");
            Logger.Log(new LogSeg("1) "), new LogSeg("Extract textures from wiiu_textures", ConsoleColor.Gray));
            if (Console.ReadKey().Key == ConsoleKey.D1)
            {
                ShowExtract();
            }
            else
            {
                Console.WriteLine();
                Logger.Error("Invalid option\n");
                ShowOptions();
            }
        }

        private static void ShowExtract()
        {
            Logger.Log(new LogSeg("\nDrag and drop your wiiu_textures file here and press enter:"));
            string input = Console.ReadLine();
            if (input.Length < 4)
            { // Fail safe
                Logger.Error("Input {0} is too short to be a valid file!", input);
                ShowExtract();
            }

            if (input[0] == '"') input = input.Substring(1, (input[input.Length - 1] == '"') ? input.Length - 2 : input.Length - 1); // Strip speech marks

            if (File.Exists(input))
            {
                WiiUTextures.Extract(new WUTExtractSettings()
                {
                    InputLocation = input,
                    OutputLocation = GetOutputFromInput(input),
                    ShouldDeswizzle = true
                });
            }
            else
            {
                Logger.Error("File {0} does not exist!\n", input);
                ShowExtract();
            }
        }
    }
}