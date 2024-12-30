using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;


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
        public byte Unknown;
        public byte Unknown2;

        public byte TPage;
        public byte ClutIndex;
    }

    private const float SCALE = 256.0f;

    private struct Vertex
    {
        public short X, Z, Y, W;
        
        public Vector3 ToVector3() {return new Vector3(X/SCALE, Z/SCALE, Y/SCALE); }
    }
    
    private WmxSegment ReadSegment(BinaryReader br)
    {
        WmxSegment segment = new WmxSegment();
        uint currentPosition = (uint)br.BaseStream.Position;
        
        uint blockId = br.ReadUInt32();
        uint[] blockOffsets = new uint[16];
        for (int i = 0; i < 16; i++) blockOffsets[i] = br.ReadUInt32();
        
        List<int> trianglesIncreasing = new List<int>();
        List<Vector3> verticesModel = new List<Vector3>();
        List<Vector2> uvsModel = new List<Vector2>();

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
                    Unknown = br.ReadByte(), Unknown2 = br.ReadByte()
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

                UV1.y = 1.0f - UV1.y;
                UV2.y = 1.0f - UV2.y;
                UV3.y = 1.0f - UV3.y;
                
                uvsModel.Add(UV1);
                uvsModel.Add(UV2);
                uvsModel.Add(UV3);
            }
        }
        
        segment.vertices = verticesModel.ToArray();
        segment.triangles = trianglesIncreasing.ToArray();
        segment.vertexCount = (ushort)verticesModel.Count;
        segment.triangleCount = (ushort)trianglesIncreasing.Count;
        segment.UVs = uvsModel.ToArray();
        return segment;
    }

    
    private GameObject CreateSegmentMesh(WmxSegment segment, int index, AssetImportContext ctx)
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

        // Set mesh data
        Debug.Log($"Setting up mesh data for segment {index}... Vertices: {segment.vertices.Length}, Triangles: {segment.triangles.Length}");
        mesh.SetVertices(segment.vertices);
        mesh.SetTriangles(segment.triangles, 0);
        mesh.SetUVs(0, segment.UVs);
        // mesh.vertices = segment.vertices;
        // mesh.triangles = segment.triangles;
        
        // Recalculate mesh properties
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        
        Debug.Log($"mesh.vertexCount: {mesh.vertexCount}, mesh.triangles.Length: {mesh.triangles.Length}");

        // Assign mesh to filter
        meshFilter.sharedMesh = mesh;
        //meshFilter.mesh = mesh;

        // Create default material
        Material material = new Material(Shader.Find("Standard"));
        meshRenderer.sharedMaterial = material;
        
        ctx.AddObjectToAsset($"mesh_{index}", mesh);
        ctx.AddObjectToAsset($"material_{index}", material);

        return segmentObj;
    }
    
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

        // Read all segments
        for (int i = 0; i < WMX_SEGMENTS; i++)
        {
            fs.Seek(WMX_SEGMENT_SIZE*i, SeekOrigin.Begin);
            segments[i] = ReadSegment(br);
            
            // Create segment mesh if it contains data
            if (segments[i].vertexCount > 0)
            {
                GameObject segmentObj = CreateSegmentMesh(segments[i], i, ctx);
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
