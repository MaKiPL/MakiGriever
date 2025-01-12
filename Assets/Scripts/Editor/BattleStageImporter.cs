using System;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1,"x")]
public class BattleStageImporter : ScriptedImporter
{
    
    private readonly int[] _x5D4 = {4,5,9,12,13,14,15,21,22,23,24,26,
        29,32,33,34,35,36,39,40,50,53,55,61,62,63,64,65,66,67,68,69,70,
        71,72,73,75,78,82,83,85,86,87,88,89,90,91,94,96,97,98,99,100,105,
        106,121,122,123,124,125,126,127,135,138,141,144,145,148,149,150,
        151,158,160};

    private readonly int[] _x5D8 = {
        0,1,2,3,6,7,10,11,17,18,25,27,28,38,41,42,43,47,49,57,58,59,60,74,
        76,77,80,81,84,93,95,101,102,103,104,109,110,111,112,113,114,115,116,
        117,118,119,120,128,129,130,131,132,133,134,139,140,143,146,152,153,154,
        155,156,159,161,162};
    private uint GetCameraPointer(int scenario)
    {
        var _5d4 = _x5D4.Any(x => x == scenario);
        var _5d8 = _x5D8.Any(x => x == scenario);
        if (_5d4) return 0x5D4;
        if (_5d8) return 0x5D8;
        switch (scenario)
        {
            case 8:
            case 48:
            case 79:
                return 0x618;

            case 16:
                return 0x628;

            case 19:
                return 0x644;

            case 20:
                return 0x61c;

            case 30:
            case 31:
                return 0x934;

            case 37:
                return 0xcc0;

            case 44:
            case 45:
            case 46:
                return 0x9A4;

            case 51:
            case 52:
            case 107:
            case 108:
                return 0x600;

            case 54:
            case 56:
                return 0x620;

            case 92:
                return 0x83c;

            case 136:
                return 0x5fc;

            case 137:
                return 0xFDC;

            case 142:
                return 0x183C;

            case 147:
                return 0x10f0;

            case 157:
                return 0x638;
        }
        throw new Exception("0xFFF, unknown pointer!");
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
        
        
    }
    
}
