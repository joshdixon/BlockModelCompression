using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class VoxelTerrain : MonoBehaviour
{
    public int terrainWidth;
    public int terrainHeight;
    public int terrainDepth;

    public int numVoxelTypes = 4;

    public VoxelTypeProbability[,,] voxelProbabilities;
    public Mesh mesh;
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;

    // Start is called before the first frame update
    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();

        voxelProbabilities = new VoxelTypeProbability[terrainWidth, terrainHeight, terrainDepth];
        GenerateProbabilitiesSphere();

        GenerateMesh();
    }

    public int sphereRadius;
    public Vector3Int sphereCenter;
    void GenerateProbabilitiesSphere()
    {
        int sphereRadiusSquared = sphereRadius * sphereRadius;

        for (int x = 0; x < terrainWidth; x++)
        {
            for (int y = 0; y < terrainHeight; y++)
            {
                for (int z = 0; z < terrainDepth; z++)
                {
                    VoxelTypeProbability probability = new VoxelTypeProbability();
                    probability.Probabilities = new float[numVoxelTypes];

                    Vector3Int voxelPosition = new Vector3Int(x, y, z);

                    float squareDistance = (sphereCenter - voxelPosition).sqrMagnitude;
                    if (squareDistance <= sphereRadiusSquared)
                    {
                        for (int i = 0; i < numVoxelTypes; i++)
                            probability.Probabilities[i] = Random.Range(0f, 1f);
                    }
                    else
                    {
                        for (int i = 0; i < numVoxelTypes; i++)
                            probability.Probabilities[i] = 0;
                    }

                    voxelProbabilities[x, y, z] = probability;
                }
            }
        }
    }

    void GenerateMesh()
    {
        mesh = new Mesh();

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        for (int x = 0; x < terrainWidth; x++)
        {
            for (int y = 0; y < terrainHeight; y++)
            {
                for (int z = 0; z < terrainDepth; z++)
                {
                    VoxelTypeProbability probability = voxelProbabilities[x, y, z];
                    if (probability.Probabilities[0] == 0)
                        continue;

                    int voxelType = GetVoxelType(x, y, z);

                    //Left side
                    if (IsFaceVisible(x - 1, y, z))
                        BuildFace(voxelType, new Vector3(x, y, z), Vector3.up, Vector3.forward, false, verts, uvs, tris);
                    //Right side
                    if (IsFaceVisible(x + 1, y, z))
                        BuildFace(voxelType, new Vector3(x + 1, y, z), Vector3.up, Vector3.forward, true, verts, uvs, tris);

                    //Bottom side
                    if (IsFaceVisible(x, y - 1, z))
                        BuildFace(voxelType, new Vector3(x, y, z), Vector3.forward, Vector3.right, false, verts, uvs, tris);
                    //Top side
                    if (IsFaceVisible(x, y + 1, z))
                        BuildFace(voxelType, new Vector3(x, y + 1, z), Vector3.forward, Vector3.right, true, verts, uvs, tris);

                    //Back side
                    if (IsFaceVisible(x, y, z - 1))
                        BuildFace(voxelType, new Vector3(x, y, z), Vector3.up, Vector3.right, true, verts, uvs, tris);
                    //Front side
                    if (IsFaceVisible(x, y, z + 1))
                        BuildFace(voxelType, new Vector3(x, y, z + 1), Vector3.up, Vector3.right, false, verts, uvs, tris);
                }
            }
        }

        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }

    void BuildFace(int voxelType, Vector3 corner, Vector3 up, Vector3 right, bool reversed, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {
        int index = verts.Count;

        verts.Add(corner);
        verts.Add(corner + up);
        verts.Add(corner + up + right);
        verts.Add(corner + right);

        float offset = 0.02f;

        Vector2 uvWidth = new Vector2(0.25f - offset * 2, 0.25f - offset * 2);
        Vector2 uvCorner = new Vector2(0f + offset, 0.75f + offset);

        uvCorner.x = 0.25f * voxelType + offset;

        uvs.Add(uvCorner);
        uvs.Add(new Vector2(uvCorner.x, uvCorner.y + uvWidth.y));
        uvs.Add(new Vector2(uvCorner.x + uvWidth.x, uvCorner.y + uvWidth.y));
        uvs.Add(new Vector2(uvCorner.x + uvWidth.x, uvCorner.y));

        if (reversed)
        {
            tris.Add(index + 0);
            tris.Add(index + 1);
            tris.Add(index + 2);
            tris.Add(index + 2);
            tris.Add(index + 3);
            tris.Add(index + 0);
        }
        else
        {
            tris.Add(index + 1);
            tris.Add(index + 0);
            tris.Add(index + 2);
            tris.Add(index + 3);
            tris.Add(index + 2);
            tris.Add(index + 0);
        }
    }

    bool IsFaceVisible(int neighbourX, int neighbourY, int neighbourZ)
    {
        if (GetVoxelType(neighbourX, neighbourY, neighbourZ) != -1)
            return false;

        return true;
    }

    int GetVoxelType(int x, int y, int z)
    {
        if ((x < 0) || (y < 0) || (z < 0) || (x >= terrainWidth) || (y >= terrainHeight) || (z >= terrainDepth))
            return -1;

        int maxIndex = -1;
        float maxProbability = 0;

        for (int i = 0; i < numVoxelTypes; i++)
        {
            if (voxelProbabilities[x, y, z].Probabilities[i] > maxProbability)
            {
                maxIndex = i;
                maxProbability = voxelProbabilities[x, y, z].Probabilities[i];
            }
        }

        return maxIndex;
    }
}
