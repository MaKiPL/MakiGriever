using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;
using PlasticGui.WorkspaceWindow.Items;

//TODO
//1. Proper shader, or at least set the params to specular =0 and no back-face cull (two sided)

[ScriptedImporter(1,"dat")]
public class DatImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        FileStream fs = new FileStream(ctx.assetPath, FileMode.Open, FileAccess.Read);
        BinaryReader br = new BinaryReader(fs);
        
        uint sectionCount = br.ReadUInt32();
        if (sectionCount != 0x0B)
        {
            Debug.LogError("Unsupported DAT file format (expected 0x0B sections, found " + sectionCount + ")");
            return;
        }
        
        uint skeletonPointer = br.ReadUInt32();
        uint modelPointer = br.ReadUInt32();
        uint animationPointer = br.ReadUInt32();
        uint textureAnimationPointer = br.ReadUInt32();
        uint animationSequencePointer = br.ReadUInt32();
        fs.Seek(4 * 5, SeekOrigin.Current);
        uint texturePointer = br.ReadUInt32();

        
        //Texture
        Texture2D[] tex = ReadTextures(br, texturePointer);
        if (tex == null)
        {
            Debug.LogError("Failed to read textures");
            return;
        }
        for (int idx = 0; idx < tex.Length; idx++)
        {
            tex[idx].name = $"texture_{idx}";
            ctx.AddObjectToAsset(tex[idx].name, tex[idx]);
        }
        
        
        
        br.Close();
        fs.Close();
        //ctx.AddObjectToAsset("TexArray", texArray);
        //ctx.SetMainObject(texArray);
    }

    private Texture2D[] ReadTextures(BinaryReader br, uint textureSectionPointer)
    {
        br.BaseStream.Seek(textureSectionPointer, SeekOrigin.Begin);
        uint textureCount = br.ReadUInt32();

        if (textureCount > 8)
        {
            Debug.LogError("Unsupported DAT texture count (expected reasonable amount, found " + textureCount + ")");
            return null;
        }
        
        List<Texture2D> textures = new List<Texture2D>();
        
        uint[] texturePointers = new uint[textureCount];
        for (int textureIndex = 0; textureIndex < textureCount; textureIndex++) texturePointers[textureIndex] = br.ReadUInt32();

        for (int textureIndex = 0; textureIndex < textureCount; textureIndex++)
        {
            br.BaseStream.Seek(textureSectionPointer + texturePointers[textureIndex], SeekOrigin.Begin);
            TimTexture tim = new();
            tim.Parse(br);
            textures.Add(tim.CreateTexture(0));
        }

        return textures.ToArray();
    }
}
