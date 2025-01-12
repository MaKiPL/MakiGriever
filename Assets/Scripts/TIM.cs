using System.IO;
using UnityEngine;

public class TimTexture
{
    
    public byte[] RawImageData { get; private set; }
    public Color[][] Cluts { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public void Parse(BinaryReader br)
    {
        // Validate TIM header
        uint magic = br.ReadUInt32();
        if (magic != 16) //0x10000000
        {
            Debug.LogError($"Invalid TIM header. Expected 0x10000000, got {magic}");
            return;
        }

        // Validate BPP
        uint bpp = br.ReadUInt32();
        if (bpp != 0x09)
        {
            Debug.LogError($"Unsupported TIM format. Expected 0x09, got {bpp}");
            return;
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
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            byte pixelColor = br.ReadByte();
            RawImageData[x+y*Width] = pixelColor;
        }
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
}
