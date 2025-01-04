using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;
using System.Linq;


//TODO:
//1. eeeee
//2. Proper TPage/ texture index for each polygon (Currently it's the whole segment draining one material)

[ScriptedImporter(1,"wmx")]
public class WmxImporter : ScriptedImporter
{
    //Number of segments that create whole worldmap
    private const int WM_REAL_SEGMENTS = 768;
    //Number of all segments inside the file - the additional ones are the alternatives of the main segments
    private const int WMX_SEGMENTS = 835;
    private const int WMX_SEGMENT_SIZE = 0x9000;
    
    private struct WmxSegment
    {
        public ushort vertexCount;
        public ushort triangleCount;
        public Vector3[] vertices;
        public int[] triangles;
        public Vector2[] UVs;
        public int[] TPageIndex;
        public Texflags[] TexFlags;
    }

    private struct Polygon
    {
        public byte F1, F2, F3;
        public byte N1, N2, N3;
        public byte U1, V1;
        public byte U2, V2;
        public byte U3, V3;
        public byte TPageClut;
        public byte GroundType;
        public byte TexFlag;
        public byte Unknown2;

        public byte TPage;
        public byte ClutIndex;
    }

    private const float SCALE = 256.0f;

    [Flags]
    public enum Texflags : byte
    {
        TEXFLAGS_SHADOW = 0b11,
        TEXFLAGS_UNK = 0b100,
        TEXFLAGS_ISENTERABLE = 0b00001000,
        TEXFLAGS_TRANSPARENT = 0b00010000,
        TEXFLAGS_ROAD = 0b00100000,
        TEXFLAGS_WATER = 0b01000000,
        TEXFLAGS_MISC = 0b10000000,
        TEXFLAGS_NORMAL = 0b0
    }

    private struct Vertex
    {
        public short X, Z, Y, W;
        
        public Vector3 ToVector3() {return new Vector3(X/SCALE, Z/SCALE, Y/SCALE); }
    }
    
    private WmxSegment ReadSegment(BinaryReader br)
    {
        WmxSegment segment = new WmxSegment();
        uint currentPosition = (uint)br.BaseStream.Position;

        int segmentId = (int)(currentPosition / WMX_SEGMENT_SIZE);
        
        uint blockId = br.ReadUInt32();
        uint[] blockOffsets = new uint[16];
        for (int i = 0; i < 16; i++) blockOffsets[i] = br.ReadUInt32();
        
        List<int> trianglesIncreasing = new List<int>();
        List<Vector3> verticesModel = new List<Vector3>();
        List<Vector2> uvsModel = new List<Vector2>();
        List<int> TPages = new List<int>();
        List<Texflags> TextureFlags = new List<Texflags>();

        for (int blockIndex = 0; blockIndex < 16; blockIndex++)
        {
            br.BaseStream.Seek(currentPosition+blockOffsets[blockIndex], SeekOrigin.Begin);
            byte polygonCount = br.ReadByte();
            byte vertexCount = br.ReadByte();
            byte normalCount = br.ReadByte();
            byte padding = br.ReadByte();
            
            Polygon[] polygons = new Polygon[polygonCount];
            for (int polygonIndex = 0; polygonIndex < polygonCount; polygonIndex++)
            {
                polygons[polygonIndex] = new Polygon
                {
                    F1 = br.ReadByte(), F2 = br.ReadByte(), F3 = br.ReadByte(),
                    N1 = br.ReadByte(), N2 = br.ReadByte(), N3 = br.ReadByte(),
                    U1 = br.ReadByte(), V1 = br.ReadByte(),
                    U2 = br.ReadByte(), V2 = br.ReadByte(),
                    U3 = br.ReadByte(), V3 = br.ReadByte(),
                    TPageClut = br.ReadByte(), GroundType = br.ReadByte(),
                    TexFlag = br.ReadByte(), Unknown2 = br.ReadByte()
                };
                
                polygons[polygonIndex].TPage = (byte)(polygons[polygonIndex].TPageClut >> 4);
                polygons[polygonIndex].ClutIndex = (byte)(polygons[polygonIndex].TPageClut & 0xF);
            }
            
            // Read vertex and normal data (do we need normals actually?)
            
            Vertex[] vertices = new Vertex[vertexCount];
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
                vertices[vertexIndex] = new Vertex
                {
                    X = br.ReadInt16(), Z = (short)-br.ReadInt16(), Y = br.ReadInt16(), W = br.ReadInt16()
                };

            int blockXOffset = blockIndex % 4;
            int blockYOffset = blockIndex / 4;
            float vertexXOffset = blockXOffset * 8.0f; //??
            float vertexYOffset = blockYOffset * -8.0f; //??



            for (int polygonIndex = 0; polygonIndex < polygonCount; polygonIndex++)
            {
                int baseIndex = verticesModel.Count;
                
                Vector3 Vertex1 = vertices[polygons[polygonIndex].F1].ToVector3() + new Vector3(vertexXOffset, 0f,vertexYOffset);
                Vector3 Vertex2 = vertices[polygons[polygonIndex].F2].ToVector3() + new Vector3(vertexXOffset,0f, vertexYOffset);
                Vector3 Vertex3 = vertices[polygons[polygonIndex].F3].ToVector3() + new Vector3(vertexXOffset,0f, vertexYOffset);
                
                verticesModel.Add(Vertex1);
                verticesModel.Add(Vertex2);
                verticesModel.Add(Vertex3);
                
                trianglesIncreasing.Add(baseIndex);
                trianglesIncreasing.Add(baseIndex+1);
                trianglesIncreasing.Add(baseIndex+2);

                Vector2 UV1 = new Vector2(polygons[polygonIndex].U1 / 255.0f, polygons[polygonIndex].V1 / 255.0f);
                Vector2 UV2 = new Vector2(polygons[polygonIndex].U2 / 255.0f, polygons[polygonIndex].V2 / 255.0f);
                Vector2 UV3 = new Vector2(polygons[polygonIndex].U3 / 255.0f, polygons[polygonIndex].V3 / 255.0f);

                TextureFlags.Add((Texflags)polygons[polygonIndex].TexFlag);
                
                UV1.y = 1.0f - UV1.y;
                UV2.y = 1.0f - UV2.y;
                UV3.y = 1.0f - UV3.y;

                if (TextureFlags.Last().HasFlag(Texflags.TEXFLAGS_ROAD))
                {
                    UV1 -= new Vector2(0f, 0.002f);
                    UV2 -= new Vector2(0f, 0.002f);
                    UV3 -= new Vector2(0f, 0.002f);
                }
                
                uvsModel.Add(UV1);
                uvsModel.Add(UV2);
                uvsModel.Add(UV3);
                
                TPages.Add(polygons[polygonIndex].TPage);
            }
        }
        
        segment.vertices = verticesModel.ToArray();
        segment.triangles = trianglesIncreasing.ToArray();
        segment.vertexCount = (ushort)verticesModel.Count;
        segment.triangleCount = (ushort)trianglesIncreasing.Count;
        segment.UVs = uvsModel.ToArray();
        segment.TPageIndex = TPages.ToArray();
        segment.TexFlags = TextureFlags.ToArray();
        return segment;
    }

    
    private GameObject CreateSegmentMesh(WmxSegment segment, int index, AssetImportContext ctx, Material[] materials)
    {
        GameObject segmentObj = new GameObject($"Segment_{index}");
        
        // Add mesh components
        MeshFilter meshFilter = segmentObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = segmentObj.AddComponent<MeshRenderer>();

        // Create and configure mesh
        Mesh mesh = new Mesh();
        mesh.name = $"WMX_Segment_{index}";
        
        // Check if mesh data exceeds 16-bit limit
        if (segment.vertexCount > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }
        
        // Group triangles by TPageIndex
        Dictionary<int, List<int>> materialToTriangles = new Dictionary<int, List<int>>();
    
        // Each triangle consists of 3 indices
        for (int i = 0; i < segment.triangles.Length; i += 3)
        {
            int tpageIndex = segment.TPageIndex[i/3]; // Assuming TPageIndex has one entry per triangle
            if (segment.TexFlags[i / 3].HasFlag(Texflags.TEXFLAGS_WATER))
                tpageIndex = 20;
            if (segment.TexFlags[i / 3].HasFlag(Texflags.TEXFLAGS_ROAD))
                tpageIndex = 21;
            
            
        
            if (!materialToTriangles.ContainsKey(tpageIndex))
            {
                materialToTriangles[tpageIndex] = new List<int>();
            }
        
            // Add the three vertices of this triangle
            materialToTriangles[tpageIndex].Add(segment.triangles[i]);
            materialToTriangles[tpageIndex].Add(segment.triangles[i + 1]);
            materialToTriangles[tpageIndex].Add(segment.triangles[i + 2]);
        }


        // Set mesh data
        Debug.Log($"Setting up mesh data for segment {index}... Vertices: {segment.vertices.Length}, Triangles: {segment.triangles.Length}");
        mesh.SetVertices(segment.vertices);
        mesh.SetUVs(0, segment.UVs);
        // mesh.vertices = segment.vertices;
        // mesh.triangles = segment.triangles;
        
        // Set submeshes
        mesh.subMeshCount = materialToTriangles.Count;
        int submeshIndex = 0;
        Material[] thisMaterials = new Material[materialToTriangles.Count];
        
        foreach (var kvp in materialToTriangles)
        {
            mesh.SetTriangles(kvp.Value.ToArray(), submeshIndex);
            thisMaterials[submeshIndex] = materials[kvp.Key];
            submeshIndex++;
        }
        
        //mesh.SetTriangles(segment.triangles, 0);
        
        // Recalculate mesh properties
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        
        Debug.Log($"mesh.vertexCount: {mesh.vertexCount}, mesh.triangles.Length: {mesh.triangles.Length}");

        // Assign mesh to filter
        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterials = thisMaterials;
        //meshFilter.mesh = mesh;

        // Create default material
        
        ctx.AddObjectToAsset($"mesh_{index}", mesh);

        return segmentObj;
    }

    private static Dictionary<int, int> interZoneMap = new Dictionary<int, int>
    {
        {361, 834},
        {327, 829},
        {274,827},
        {275,828},
        {267,826},
        {149,824},
        {150,825},
        {214,830},
        {215,831},
        {246,832},
        {247,833},
        
    };
    
    public override void OnImportAsset(AssetImportContext ctx)
    {
        using FileStream fs = File.OpenRead(ctx.assetPath);
        using BinaryReader br = new BinaryReader(fs);
        
        // Create root GameObject
        GameObject root = new GameObject("WMX_WorldMap");
        
        // List to store all segments
        WmxSegment[] segments = new WmxSegment[WMX_SEGMENTS];

        const int GRID_WIDTH = 32;
        const int GRID_HEIGHT = 24;
        
        string texlPath = Directory.GetFiles(Path.GetDirectoryName(ctx.assetPath) ?? string.Empty, "*texl*").First();
        Texture2D[] texls = Texl.ReadTexl(texlPath);
        
        
        Material[] materials = new Material[texls.Length + 3]; //ocean, road, misc
        for (int i = 0; i < texls.Length; i++)
        {
            materials[i] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            materials[i].name = $"Material_{i}";
            materials[i].SetTexture("_BaseMap", texls[i]);
            materials[i].SetFloat("_Smoothness", 0.0f);
            materials[i].SetFloat("_AlphaClip", 1.0f);
            ctx.AddObjectToAsset($"texl_{i}", texls[i]);
            ctx.AddObjectToAsset($"material_{i}", materials[i]);
        }

        int materialIndex = texls.Length;
        //Ocean
        materials[materialIndex] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        materials[materialIndex].name = $"Material_{texls.Length}_ocean";
        materials[materialIndex].SetFloat("_Smoothness", 0.0f);
        ctx.AddObjectToAsset($"material_{materialIndex}", materials[materialIndex]);

        materialIndex++;
        //Road
        materials[materialIndex] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        materials[materialIndex].name = $"Material_{materialIndex}_road";
        materials[materialIndex].SetFloat("_Smoothness", 0.0f);
        ctx.AddObjectToAsset($"material_{materialIndex}", materials[materialIndex]);

        materialIndex++;
        //Misc
        materials[materialIndex] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        materials[materialIndex].name = $"Material_{materialIndex}_misc";
        materials[materialIndex].SetFloat("_Smoothness", 0.0f);
        ctx.AddObjectToAsset($"material_{materialIndex}", materials[materialIndex]);
        

        // Read all segments
        for (int i = 0; i < WMX_SEGMENTS; i++)
        {
            int baseInterZone = i;
            if (interZoneMap.TryGetValue(i, out int interZone))
                baseInterZone = interZone;

            fs.Seek(WMX_SEGMENT_SIZE*baseInterZone, SeekOrigin.Begin);
            segments[i] = ReadSegment(br);
            
            // Create segment mesh if it contains data
            if (segments[i].vertexCount > 0)
            {
                GameObject segmentObj = CreateSegmentMesh(segments[i], i, ctx, materials);
                //var mr = segmentObj.GetComponent<MeshRenderer>();
                //mr.material = materials[segments[i].TPageIndex[0]];
                segmentObj.transform.parent = root.transform;
                if (i < WM_REAL_SEGMENTS)
                {
                    int gridX = i % GRID_WIDTH;
                    int gridY = i / GRID_WIDTH;
                    segmentObj.transform.localPosition = new Vector3(gridX * GRID_WIDTH, 0f, gridY * -GRID_WIDTH);
                }
                ctx.AddObjectToAsset($"segment_{i}", segmentObj);
            }
        }

        // Add the root object to the import context
        ctx.AddObjectToAsset("root", root);
        ctx.SetMainObject(root);
    }
    
}
