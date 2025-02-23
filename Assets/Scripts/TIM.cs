using System.IO;
using UnityEngine;

public class TimTexture
{
    
    public byte[] RawImageData { get; private set; }
    public Color[][] Cluts { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public bool Parse(BinaryReader br)
    {
        // Validate TIM header
        uint magic = br.ReadUInt32();
        if (magic != 16) //0x10000000
        {
            Debug.LogError($"Invalid TIM header. Expected 0x10000000, got {magic}");
            return false;
        }

        // Validate BPP
        uint bpp = br.ReadUInt32();
        if (bpp != 0x09)
        {
            Debug.LogError($"Unsupported TIM format. Expected 0x09, got {bpp}");
            return false;
        }

        // Read CLUT section
        ushort clutSectionSize = br.ReadUInt16();
        br.BaseStream.Seek(6, SeekOrigin.Current); // Skip unused data
        
        ushort clutColorCount = br.ReadUInt16();
        ushort clutCount = br.ReadUInt16();

        // Parse CLUTs
        Cluts = new Color[clutCount][];
        for (int clutIndex = 0; clutIndex < clutCount; clutIndex++)
        {
            Cluts[clutIndex] = new Color[clutColorCount];
            for (int colorIndex = 0; colorIndex < clutColorCount; colorIndex++)
            {
                Cluts[clutIndex][colorIndex] = ReadPsxColor(br.ReadUInt16());
            }
        }

        // Read image section header
        ushort textureSectionSize = br.ReadUInt16();
        br.BaseStream.Seek(6, SeekOrigin.Current); // Skip unused data

        Width = (ushort)(br.ReadUInt16() * 2);
        Height = br.ReadUInt16();

        // Read image data
        RawImageData = new byte[Width * Height];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            byte pixelColor = br.ReadByte();
            int destIndex = x + (Height - 1 - y) * Width;
            RawImageData[destIndex] = pixelColor;
        }

        return true;
    }

    private static Color ReadPsxColor(ushort pixelColor)
    {
        // PSX 16-bit color format: 0bMBBBBBGGGGGRRRRR where M is mask bit
        byte r = (byte)((pixelColor & 0x1F) << 3);
        byte g = (byte)((pixelColor >> 5 & 0x1F) << 3);
        byte b = (byte)((pixelColor >> 10 & 0x1F) << 3);

        // Improve color precision
        r |= (byte)(r >> 5);
        g |= (byte)(g >> 5);
        b |= (byte)(b >> 5);

        // Set alpha - transparent if black, opaque otherwise
        byte a = (byte)((r == 0 && g == 0 && b == 0) ? 0 : 255);

        return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }


    public Texture2D CreateTexture(int clutIndex = 0)
    {
        Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
        Color[] colors = new Color[Width * Height];
        
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int sourceIndex = x + y * Width;
                byte colorIndex = RawImageData[sourceIndex];
                
                if (clutIndex < Cluts.Length && colorIndex < Cluts[clutIndex].Length)
                {
                    colors[sourceIndex] = Cluts[clutIndex][colorIndex];
                }
            }
        }

        texture.SetPixels(colors);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        return texture;
    }
}
