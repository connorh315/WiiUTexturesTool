# WiiUTexturesTool

Command-line tool for extracting wiiu_textures files from Wii-U LEGO Dimensions!

# Usage

1. Ensure you have .NET 6.0 installed.

2. Download the latest version, use WiiUTexturesTool_win if you're on Windows and WiiUTexturesTool_mac if you're on macOS.

3. Drag-and-drop the wiiu_textures file onto WiiUTexturesTool and it will extract into a folder in the same location as the wiiu_textures file with the same name as the wiiu_textures file.

OR you can open the tool and use the menu in the console.

## Extracting multiple wiiu_textures files
You can extract multiple wiiu_textures files at a time by selecting multiple wiiu_textures files and dragging them on top of WiiUTexturesTool.

# Advanced

The tool supports command line arguments:

## Extracting to a specific folder:

The following command will extract "1wizardofoza_nxg.wiiu_textures" to a specific folder "TexturedDump":

`WiiUTexturesTool.exe C:\Users\Connor\Documents\1wizardofoza_nxg.wiiu_textures C:\Users\Connor\Documents\TextureDump`

## Disabling deswizzling:

If you know what you're doing and would like to extract all textures "as-is" in the file and not deswizzle them, then you can use disable-deswizzle anywhere, with any number of arguments:

`WiiUTexturesTool.exe C:\Users\Connor\Documents\1wizardofoza_nxg.wiiu_textures --disable-deswizzle`
