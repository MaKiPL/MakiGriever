using System.IO;
using UnityEngine;

public static class Texl
{
    private const int TEXTURE_COUNT = 20;
    private const int TEXTURE_SECTOR_SIZE = 0x12800;

    public static Texture2D[] ReadTexl(string assetPath)
    {
        using FileStream fs = File.OpenRead(assetPath);
        using BinaryReader br = new BinaryReader(fs);

        Texture2D[] textures = new Texture2D[TEXTURE_COUNT];
        for (int index = 0; index < TEXTURE_COUNT; index++)
        {
            fs.Seek(TEXTURE_SECTOR_SIZE * index, SeekOrigin.Begin);

            TimTexture tim = new TimTexture();
            tim.Parse(br);
            
            //Main image data
            Texture2D sectorTexture = new Texture2D(tim.Width, tim.Height, TextureFormat.RGBA32, false, false);

            Color[] textureColors = new Color[tim.Width * tim.Height];
            for(int x = 0; x < tim.Width; x++)
            for (int y = 0; y < tim.Height; y++)
            {
                byte pixelColor = tim.RawImageData[y + (tim.Width - 1 - x) * tim.Height];
                int xIndex = (int)(y / 64.0); // Back to original
                int yIndex = (int)(x / 64.0); // Back to original
                int finalClutIndex = yIndex * 4 + xIndex;
                textureColors[y + (tim.Width - 1 - x) * tim.Height] = tim.Cluts[finalClutIndex][pixelColor];
            }
        
            sectorTexture.SetPixels(textureColors);
            sectorTexture.filterMode = FilterMode.Point;

            sectorTexture.name = $"Texl_{index}";
            
            // Create a material for each texture
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = $"TexlMaterial_{index}";
            material.mainTexture = sectorTexture;
            
            sectorTexture.Apply();
            
            textures[index] = sectorTexture;
        }

        return textures;
    }
}
