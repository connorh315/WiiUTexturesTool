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
                InputLocation = @"A:\Dimensions\EXTRACT\LEVELS\STORY\1WIZARDOFOZ\1WIZARDOFOZA\1WIZARDOFOZA_NXG.WIIU_TEXTURES",
                OutputLocation = @"A:\Dimensions\testoutput\",
                ShouldDeswizzle = true
            };
#else
            // Put something clever here to de-struct the args
#endif

            Extractor.Extract(settings);
        }
    }
}