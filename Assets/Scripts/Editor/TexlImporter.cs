using System;
using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;

//TODO
//1. Proper shader, or at least set the params to specular =0 and no back-face cull (two sided)

[ScriptedImporter(1,"texl")]
public class TexlImporter : ScriptedImporter
{
    private const int TEXTURE_COUNT = 20;
    private const int TEXTURE_SECTOR_SIZE = 0x12800;
    
    public override void OnImportAsset(AssetImportContext ctx)
    {
        using FileStream fs = File.OpenRead(ctx.assetPath);
        using BinaryReader br = new BinaryReader(fs);

        Texture2D[] textures = new Texture2D[TEXTURE_COUNT];

        for (int index = 0; index < TEXTURE_COUNT; index++)
        {
            fs.Seek(TEXTURE_SECTOR_SIZE * index, SeekOrigin.Begin);
            
            uint magic = br.ReadUInt32();
            if(magic != 16) //0x10000000
            {
                Debug.LogError($"Invalid texture sector header at index {index}. Expected 0x10000000, got {magic}");
                return;
            }

            uint bpp = br.ReadUInt32();
            if (bpp != 0x09)
            {
                Debug.LogError($"Unsupported texture format at index {index}. Expected 0x09, got {bpp}");
                return;
            }
            
            ushort clutSectionSize = br.ReadUInt16();
            
            fs.Seek(6, SeekOrigin.Current); //skip crap
            
            ushort clutColorCount = br.ReadUInt16();
            ushort clutCount = br.ReadUInt16();
            
            
            //CLUT
            Color[][] clut = new Color[clutCount][];
            for (int clutIndex = 0; clutIndex < clutCount; clutIndex++)
            {
                clut[clutIndex] = new Color[clutColorCount];
                for (int colorIndex = 0; colorIndex < clutColorCount; colorIndex++)
                {
                    ushort pixelColor = br.ReadUInt16();
                    
                    // PSX 16-bit color format is 0bMBBBBBGGGGGRRRRR where M is mask bit
                    byte r = (byte)((pixelColor & 0x1F) << 3);        // 5 bits for Red   (0-31) << 3 to scale to (0-255)
                    byte g = (byte)((byte)((pixelColor >> 5) & 0x1F) << 3);   // 5 bits for Green (0-31) << 3 to scale to (0-255)
                    byte b = (byte)((byte)((pixelColor >> 10) & 0x1F) << 3);  // 5 bits for Blue  (0-31) << 3 to scale to (0-255)
        
                    // Optional: Improve color precision by filling lower bits
                    r |= (byte)(r >> 5);
                    g |= (byte)(g >> 5);
                    b |= (byte)(b >> 5);
        
                    // Set alpha - transparent if black, opaque otherwise
                    byte a = (byte)((r == 0 && g == 0 && b == 0) ? 0 : 255);


                    clut[clutIndex][colorIndex] = new Color(r / 255.0f, g / 255.0f, b / 255.0f, a/255.0f);
                }
            }
            //EOF clut
            
            ushort textureSectionSize = br.ReadUInt16();
            fs.Seek(6, SeekOrigin.Current); //skip another unwanted crap

            ushort width = (ushort)(br.ReadUInt16() * 2);
            ushort height = br.ReadUInt16();
            
            //Main image data
            Texture2D sectorTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);

            Color[] textureColors = new Color[width * height];
            for(int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                byte pixelColor = br.ReadByte();
                int xIndex = (int)(y / 64.0); // Back to original
                int yIndex = (int)(x / 64.0); // Back to original
                int finalClutIndex = yIndex * 4 + xIndex;
                // Try rotating clockwise by storing in a different order
                textureColors[y + (width - 1 - x) * height] = clut[finalClutIndex][pixelColor];
            }
        
            sectorTexture.SetPixels(textureColors);

            sectorTexture.name = $"Texl_{index}";
            
            // Create a material for each texture
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = $"TexlMaterial_{index}";
            material.mainTexture = sectorTexture;
            
            sectorTexture.Apply();
            ctx.AddObjectToAsset($"texture_{index}", sectorTexture);
            ctx.AddObjectToAsset($"material_{index}", material);
            textures[index] = sectorTexture;
        }

        //create texture array from textures
        Texture2DArray texArray = new Texture2DArray(textures[0].width, textures[0].height, TEXTURE_COUNT, TextureFormat.RGBA32, false, false);
        for(int i = 0; i < TEXTURE_COUNT; i++)
            texArray.SetPixels32(textures[i].GetPixels32(), i);
        
        texArray.Apply();

        //write texture array to file
        ctx.AddObjectToAsset("TexArray", texArray);
        ctx.SetMainObject(texArray);
    }
    
}
