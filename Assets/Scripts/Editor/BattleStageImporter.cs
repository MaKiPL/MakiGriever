using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1,"x")]
public class BattleStageImporter : ScriptedImporter
{
    
private readonly HashSet<int> _x5D4 = new()
{
    4,5,9,12,13,14,15,21,22,23,24,26,
    29,32,33,34,35,36,39,40,50,53,55,61,62,63,64,65,66,67,68,69,70,
    71,72,73,75,78,82,83,85,86,87,88,89,90,91,94,96,97,98,99,100,105,
    106,121,122,123,124,125,126,127,135,138,141,144,145,148,149,150,
    151,158,160
};

private readonly HashSet<int> _x5D8 = new()
{
    0,1,2,3,6,7,10,11,17,18,25,27,28,38,41,42,43,47,49,57,58,59,60,74,
    76,77,80,81,84,93,95,101,102,103,104,109,110,111,112,113,114,115,116,
    117,118,119,120,128,129,130,131,132,133,134,139,140,143,146,152,153,154,
    155,156,159,161,162
    
};
private uint GetCameraPointer(int scenario)
{
    if (_x5D4.Contains(scenario)) return 0x5D4;
    if (_x5D8.Contains(scenario)) return 0x5D8;

    return scenario switch
    {
        8 or 48 or 79 => 0x618,
        16 => 0x628,
        19 => 0x644,
        20 => 0x61c,
        30 or 31 => 0x934,
        37 => 0xcc0,
        44 or 45 or 46 => 0x9A4,
        51 or 52 or 107 or 108 => 0x600,
        54 or 56 => 0x620,
        92 => 0x83c,
        136 => 0x5fc,
        137 => 0xFDC,
        142 => 0x183C,
        147 => 0x10f0,
        157 => 0x638,
        _ => throw new ArgumentException($"Unknown scenario: {scenario}", nameof(scenario))
    };
}

    public override void OnImportAsset(AssetImportContext ctx)
    {
        string battleStageName = Path.GetFileNameWithoutExtension(ctx.assetPath);
        string scenarioNumber = battleStageName.Substring(battleStageName.Length - 3, 3);
        int scenario = int.Parse(scenarioNumber);
        uint cameraPointer = GetCameraPointer(scenario);
        Debug.Log($"Parsing scenario {scenario} with camera pointer 0x{cameraPointer:X4} in {ctx.assetPath}...");
        
        using FileStream fs = new FileStream(ctx.assetPath, FileMode.Open, FileAccess.Read);
        using BinaryReader br = new BinaryReader(fs);

        fs.Seek(cameraPointer, SeekOrigin.Begin);
        ushort cameraSections = br.ReadUInt16();
        if (cameraSections != 2)
        {
            Debug.LogWarning($"Camera section is bad. Expected 2, got {cameraSections}. Skipping...");
            return;
        }

        fs.Seek(4, SeekOrigin.Current);
        ushort cameraSize = br.ReadUInt16();

        fs.Seek(cameraPointer + cameraSize, SeekOrigin.Begin);
        long modelPointer = fs.Position; 

        uint modelSections = br.ReadUInt32();
        if (modelSections != 6)
        {
            Debug.LogWarning($"Model section at {modelPointer} is bad. Expected 6, got {modelSections}. Skipping...");
            return;
        }
        
        uint group1Model = (uint)(br.ReadUInt32() + modelPointer);
        uint group2Model = (uint)(br.ReadUInt32() + modelPointer);
        uint group3Model = (uint)(br.ReadUInt32() + modelPointer);
        uint group4Model = (uint)(br.ReadUInt32() + modelPointer);
        uint texturePointer = (uint)(br.ReadUInt32() + modelPointer);
        
        //parse group 1
    }
    
}
