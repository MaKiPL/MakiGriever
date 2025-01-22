using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using PlasticGui.WorkspaceWindow.BranchExplorer;
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

private const float SCALE = 2048.0f;
private List<Material> materials;

struct GroupSegment
{
    public List<Vector3> vertices;
    public List<Vector2> uvs;
    public List<int> indices;
    public List<int> clutIds;
}

private Vector3 ReadVertex(BinaryReader br)
{
    short x = br.ReadInt16();
    short y = br.ReadInt16();
    short z = br.ReadInt16();
    
    return new Vector3(x / SCALE, y / SCALE, z / SCALE);
}

private byte GetClutID(BinaryReader br)
{
    ushort clutID = br.ReadUInt16();
    return (byte)(clutID >> 14 & 0b11 | clutID << 2 & 0b1100);
}

private Tuple<byte, byte> ReadUV(BinaryReader br)
{
    return new Tuple<byte, byte>(br.ReadByte(), br.ReadByte());
}

Vector2 TimTextureResolution;

private Vector2 TweakUV(Tuple<byte, byte> UVbyte, int tPageOffset)
{
    const float texPageWidth = 128.0f;
    float U = (UVbyte.Item1 + tPageOffset * texPageWidth) / TimTextureResolution.x;
    float V = UVbyte.Item2 / TimTextureResolution.y;
    return new Vector2(U, 1.0f-V);
}

    private GroupSegment[] ReadGroup(uint offset, BinaryReader br)
    {
        br.BaseStream.Seek(offset, SeekOrigin.Begin);
        uint sectionCount = br.ReadUInt32();
        br.BaseStream.Seek(4, SeekOrigin.Current); //skip settings_1
        uint objectListPointer = offset + br.ReadUInt32();
        br.BaseStream.Seek(objectListPointer, SeekOrigin.Begin);
        uint objectCount = br.ReadUInt32();
        if (objectCount == 0)
        {
            Debug.LogWarning("No objects found in group. Skipping...");
            return null;
        }
        
        uint[] objectPointers = new uint[objectCount];
        for (int i = 0; i < objectCount; i++) objectPointers[i] = objectListPointer + br.ReadUInt32();

        GroupSegment[] groups = new GroupSegment[objectCount];
        for (int i = 0; i < objectCount; i++)
        {
            br.BaseStream.Seek(objectPointers[i], SeekOrigin.Begin);
            
            uint header = br.ReadUInt32();
            if (header != 0x00010001)
            {
                Debug.LogWarning($"Invalid object header={header:X8} at 0x{objectPointers[i]:X8}. Skipping...");
                continue;
            }
            
            GroupSegment segment = new GroupSegment();
            
            ushort vertexCount = br.ReadUInt16();
            
            segment.vertices = new List<Vector3>(vertexCount);
            
            for (int j = 0; j < vertexCount; j++) segment.vertices.Add(ReadVertex(br));

            int seekCount = (int)(br.BaseStream.Position % 4 + 4);

            br.BaseStream.Seek(seekCount, SeekOrigin.Current);
            
            ushort triangleCount = br.ReadUInt16();
            ushort quadCount = br.ReadUInt16();

            br.BaseStream.Seek(4, SeekOrigin.Current);

            segment.clutIds = new List<int>();
            segment.indices = new List<int>();
            segment.uvs = new List<Vector2>();

            for (int n = 0; n < triangleCount; n++)
            {
                segment.indices.Add(br.ReadUInt16()); //A
                segment.indices.Add(br.ReadUInt16()); //B
                segment.indices.Add(br.ReadUInt16()); //C

                Tuple<byte,byte> UV1 = ReadUV(br);
                Tuple<byte,byte> UV2 = ReadUV(br);
                

                byte clutId = GetClutID(br);

                Tuple<byte, byte> UV3 = ReadUV(br);
                
                byte TPage = (byte)(br.ReadByte() & 0b1111);

                int tPagePixelOffset = TPage; //because 64px per TPage
                
                segment.uvs.Add(TweakUV(UV2, tPagePixelOffset));
                segment.uvs.Add(TweakUV(UV3, tPagePixelOffset));
                segment.uvs.Add(TweakUV(UV1, tPagePixelOffset));
                
                segment.clutIds.Add(clutId);

                br.BaseStream.Seek(5, SeekOrigin.Current); //skip Hide, RGB + GPU
            }

            for (int n = 0; n < quadCount; n++)
            {
                //quad is ABDC > ABD ACD 
                ushort A = br.ReadUInt16();
                ushort B = br.ReadUInt16();
                ushort C = br.ReadUInt16();
                ushort D = br.ReadUInt16();
                
                segment.indices.Add(A);
                segment.indices.Add(B);
                segment.indices.Add(D);
                
                segment.indices.Add(D);
                segment.indices.Add(C);
                segment.indices.Add(A);
                
                Tuple<byte,byte> UV1 = ReadUV(br);
                byte clutId = GetClutID(br);
                
                Tuple<byte, byte> UV2 = ReadUV(br);
                
                byte TPage = (byte)(br.ReadByte() & 0b1111);
                int tPagePixelOffset = TPage;
                
                byte hide = br.ReadByte(); //skip
                
                Tuple<byte, byte> UV3 = ReadUV(br);
                Tuple<byte, byte> UV4 = ReadUV(br);
                
                // UVs should be also ABD ACD
                segment.uvs.Add(TweakUV(UV1, tPagePixelOffset));
                segment.uvs.Add(TweakUV(UV2, tPagePixelOffset));
                segment.uvs.Add(TweakUV(UV4, tPagePixelOffset));
                
                segment.uvs.Add(TweakUV(UV4, tPagePixelOffset));
                segment.uvs.Add(TweakUV(UV3, tPagePixelOffset));
                segment.uvs.Add(TweakUV(UV1, tPagePixelOffset));

                br.BaseStream.Seek(4, SeekOrigin.Current); //RGB + GPU
            }

            groups[i] = segment;
        }

        return groups;
    }

    GameObject CreateGameObject(string goName, GroupSegment[] groups, AssetImportContext ctx)
    {
        if (groups == null || groups.Length == 0)
            return null;

        GameObject go = new GameObject(goName);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        
        MeshFilter meshFilter = go.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();
        
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        int polyIndex = 0;
        foreach (GroupSegment group in groups)
        {
            if(group.vertices == null || group.indices == null)
                continue;
            int localUvPointer = 0;
            Debug.Log($"group {group.vertices.Count} uvs: {group.uvs.Count} indices: {group.indices.Count}");
            foreach (int index in group.indices)
            {
                vertices.Add(group.vertices[index]);
                uvs.Add(group.uvs[localUvPointer++]);
                indices.Add(polyIndex);
                polyIndex++;
            }
        }
        
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(indices, 0);

        
        mesh.name = $"{goName}_Mesh";
        
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        
        
        ctx.AddObjectToAsset(mesh.name, mesh);

        meshFilter.sharedMesh = mesh;
        
        return go;
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
        uint texturePointer2 = (uint)(br.ReadUInt32() + modelPointer);
        
        Debug.Log($"Texture pointer: 0x{texturePointer:X8}");
        
        fs.Seek(texturePointer, SeekOrigin.Begin);
        TimTexture tim = new TimTexture();
        bool success = tim.Parse(br);
        if (!success)
        {
            Debug.LogWarning("Failed to parse TIM texture. Trying the second texture...");
            fs.Seek(texturePointer2, SeekOrigin.Begin);
            success = tim.Parse(br);
        }
        
        if (!success)
        {
            Debug.LogError("Failed to parse either TIM texture. Skipping...");
            return;
        }
        
        materials = new List<Material>();
        for (int clutIndex = 0; clutIndex < tim.Cluts.Length; clutIndex++)
        {
            Texture2D clutTexture = tim.CreateTexture(clutIndex);
            TimTextureResolution = new Vector2(clutTexture.width, clutTexture.height);
            string clutTextureName = $"{battleStageName}_{clutIndex}";
            clutTexture.name = clutTextureName;
            ctx.AddObjectToAsset(clutTextureName, clutTexture);
            
            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.name = $"{battleStageName}_Mat_{clutIndex}";
            material.SetTexture("_BaseMap", clutTexture);
            //material.SetFloat("_Smoothness", 0.0f);
            material.SetFloat("_AlphaClip", 1.0f);
            material.SetFloat("_Cull", 0.0f);
            
            ctx.AddObjectToAsset(material.name, material);
            materials.Add(material);
        }
        
        GroupSegment[] Group1 = ReadGroup(group1Model, br);
        GroupSegment[] Group2 = ReadGroup(group2Model, br);
        GroupSegment[] Group3 = ReadGroup(group3Model, br);
        GroupSegment[] Group4 = ReadGroup(group4Model, br);
        
        
        GameObject group1Object = CreateGameObject($"{battleStageName}_group1", Group1, ctx);
        GameObject group2Object = CreateGameObject($"{battleStageName}_group2", Group2, ctx);
        GameObject group3Object = CreateGameObject($"{battleStageName}_group3", Group3, ctx);
        GameObject group4Object = CreateGameObject($"{battleStageName}_group4", Group4, ctx);
        
        GameObject combinedObject = new GameObject($"{battleStageName}_combined");
        MeshFilter combinedMeshFilter = combinedObject.AddComponent<MeshFilter>();
        MeshRenderer combinedMeshRenderer = combinedObject.AddComponent<MeshRenderer>();
        List<CombineInstance> combineInstances = new List<CombineInstance>();
        if(group1Object != null)
        {
            ctx.AddObjectToAsset(group1Object.name, group1Object);
            combineInstances.Add(new CombineInstance { mesh = group1Object.GetComponent<MeshFilter>().sharedMesh, transform = group1Object.transform.localToWorldMatrix });
        }
        if(group2Object != null)
        {
            ctx.AddObjectToAsset(group2Object.name, group2Object);
            combineInstances.Add(new CombineInstance { mesh = group2Object.GetComponent<MeshFilter>().sharedMesh, transform = group2Object.transform.localToWorldMatrix });
        }
        if(group3Object!= null)
        {
            ctx.AddObjectToAsset(group3Object.name, group3Object);
            combineInstances.Add(new CombineInstance { mesh = group3Object.GetComponent<MeshFilter>().sharedMesh, transform = group3Object.transform.localToWorldMatrix });
        }
        if(group4Object!= null)
        {
            ctx.AddObjectToAsset(group4Object.name, group4Object);
            combineInstances.Add(new CombineInstance { mesh = group4Object.GetComponent<MeshFilter>().sharedMesh, transform = group4Object.transform.localToWorldMatrix });
        }
        
        combinedObject.transform.position = Vector3.zero;
        combinedObject.transform.rotation = Quaternion.identity;
        combinedObject.transform.localScale = Vector3.one;
        
        combinedMeshFilter.sharedMesh = new Mesh();
        combinedMeshFilter.sharedMesh.name = combinedObject.name+"Mesh";
        combinedMeshFilter.sharedMesh.CombineMeshes(combineInstances.ToArray(), true);
        combinedMeshRenderer.sharedMaterials = materials.ToArray();
        
        ctx.AddObjectToAsset(combinedMeshFilter.sharedMesh.name, combinedMeshFilter.sharedMesh);
        ctx.AddObjectToAsset(combinedObject.name, combinedObject);
        ctx.SetMainObject(combinedObject);
    }
    
}