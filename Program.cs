using ModLib;
using WiiUTexturesTool.Extract;

namespace WiiUTexturesTool
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if DEBUG
            ExtractSettings settings = new ExtractSettings()
            {
                InputLocation = @"A:\Dimensions\EXTRACT\LEVELS\STORY\1WIZARDOFOZ\1WIZARDOFOZA\TECH\1WIZARDOFOZA_TECH_NXG.WIIU_TEXTURES",
                OutputLocation = @"A:\Dimensions\EXTRACT\LEVELS\STORY\1WIZARDOFOZ\1WIZARDOFOZA\TECH\TEX",
                ShouldDeswizzle = true
            };
            Extractor.Extract(settings);
#else
            string command = args[0].ToLower();
            if (command == "extract" || command == "e")
            {
                ExtractSettings settings = new ExtractSettings()
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
                Extractor.Extract(settings);
            }
#endif
        }

        private static bool ValidateArgument(string arg, string option)
        {
            return (arg == option) || (arg == "-" + option[0]) || (arg == "--" + option[0]) || (arg == "-" + option) ||
                   (arg == "--" + option);
        }
    }
}