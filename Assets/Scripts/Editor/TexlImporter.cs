using System;
using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;

//TODO
//1. Proper shader, or at least set the params to specular =0 and no back-face cull (two sided)

[ScriptedImporter(1,"texl")]
public class TexlImporter : ScriptedImporter
{
    
    
    public override void OnImportAsset(AssetImportContext ctx)
    {
        Texture2D[] textures = Texl.ReadTexl(ctx.assetPath);
        

        //create texture array from textures
        Texture2DArray texArray = new Texture2DArray(textures[0].width, textures[0].height, textures.Length, TextureFormat.RGBA32, false, false);
        for(int i = 0; i < textures.Length; i++)
        {
            ctx.AddObjectToAsset("texture_{i}", textures[i]);
            texArray.SetPixels32(textures[i].GetPixels32(), i);
        }
        
        texArray.Apply();

        //write texture array to file
        ctx.AddObjectToAsset("TexArray", texArray);
        ctx.SetMainObject(texArray);
    }
    
}
