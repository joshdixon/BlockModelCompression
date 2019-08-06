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

    public Vector3 parentBlockSize;
    public Vector3Int subBlocksPerParent;

    public int filterLayerMin = 0;
    public int filterLayerMax = 20;

    public VoxelTypeProbability[,,] voxelProbabilities;
    public int[,,] parentBlocks;
    public int[,,] subBlocks;

    public Mesh mesh;
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();

        voxelProbabilities = new VoxelTypeProbability[terrainWidth * subBlocksPerParent.x, terrainHeight * subBlocksPerParent.y, terrainDepth * subBlocksPerParent.z];
        GenerateProbabilitiesSphere();

        CreateBlockModel();

        GenerateMesh();
    }

    public int sphereRadius;
    public int terrainLevel;
    public Vector3Int sphereCenter;
    void GenerateProbabilitiesSphere()
    {
        int sphereRadiusSquared = sphereRadius * sphereRadius;

        for (int x = 0; x < terrainWidth * subBlocksPerParent.x; x++)
        {
            for (int y = 0; y < terrainHeight * subBlocksPerParent.y; y++)
            {
                for (int z = 0; z < terrainDepth * subBlocksPerParent.z; z++)
                {
                    VoxelTypeProbability probability = new VoxelTypeProbability();
                    probability.Probabilities = new float[numVoxelTypes];

                    Vector3Int voxelPosition = new Vector3Int(x, y, z);

                    for (int i = 0; i < numVoxelTypes; i++)
                        probability.Probabilities[i] = 0;

                    if (y < terrainLevel)
                    {
                        probability.Probabilities[0] = 1;
                        for (int i = 1; i < numVoxelTypes; i++)
                            probability.Probabilities[i] = 0;
                    }

                    float squareDistance = (sphereCenter - voxelPosition).sqrMagnitude;

                    if (squareDistance <= sphereRadiusSquared && y < terrainLevel)
                    {
                        probability.Probabilities[0] = 0.01f;
                        probability.Probabilities[1] = 1f;
                        for (int i = 2; i < numVoxelTypes; i++)
                            probability.Probabilities[i] = 0;
                    }

                    voxelProbabilities[x, y, z] = probability;
                }
            }
        }
    }

    public void CreateBlockModel()
    {
        parentBlocks = new int[terrainWidth, terrainHeight, terrainDepth];
        subBlocks = new int[terrainWidth * subBlocksPerParent.x, terrainHeight * subBlocksPerParent.y, terrainDepth * subBlocksPerParent.z];

        for (int x = 0; x < terrainWidth * subBlocksPerParent.x; x++)
        {
            for (int y = 0; y < terrainHeight * subBlocksPerParent.y; y++)
            {
                for (int z = 0; z < terrainDepth * subBlocksPerParent.z; z++)
                {
                    subBlocks[x, y, z] = GetVoxelType(x, y, z);
                }
            }
        }

        for (int x = 0; x < terrainWidth; x++)
        {
            for (int y = 0; y < terrainHeight; y++)
            {
                for (int z = 0; z < terrainDepth; z++)
                {
                    parentBlocks[x, y, z] = GetParentBlockType(x, y, z);
                }
            }
        }
    }

    int GetParentBlockType(int x, int y, int z)
    {
        int firstType = GetVoxelType(x * subBlocksPerParent.x, y * subBlocksPerParent.y, z * subBlocksPerParent.z);

        for (int i = 0; i < subBlocksPerParent.x; i++)
        {
            for (int j = 0; j < subBlocksPerParent.y; j++)
            {
                for (int k = 0; k < subBlocksPerParent.z; k++)
                {
                    int subBlockX = x * subBlocksPerParent.x + i;
                    int subBlockY = y * subBlocksPerParent.y + j;
                    int subBlockZ = z * subBlocksPerParent.z + k;

                    int subBlockType = GetVoxelType(subBlockX, subBlockY, subBlockZ);
                    if (subBlockType != firstType)
                        return -2;
                }
            }
        }

        return firstType;
    }

    public void GenerateMesh()
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
                    if (y < filterLayerMin || y > filterLayerMax)
                        continue;

                    int parentBlock = parentBlocks[x, y, z];

                    if (parentBlock == -1)
                        continue;

                    if (parentBlock != -2)
                        BuildBlock(parentBlock, x, y, z, verts, uvs, tris, Vector3.one); //Build a parent block
                    else
                    {
                        for (int i = 0; i < subBlocksPerParent.x; i++)
                        {
                            for (int j = 0; j < subBlocksPerParent.y; j++)
                            {
                                for (int k = 0; k < subBlocksPerParent.z; k++)
                                {
                                    int subBlockX = x * subBlocksPerParent.x + i;
                                    int subBlockY = y * subBlocksPerParent.y + j;
                                    int subBlockZ = z * subBlocksPerParent.z + k;

                                    int subBlockType = GetVoxelType(subBlockX, subBlockY, subBlockZ);

                                    BuildBlock(parentBlock, x, y, z, verts, uvs, tris, new Vector3(1f / subBlocksPerParent.x, 1f / subBlocksPerParent.y, 1f / subBlocksPerParent.z));
                                }
                            }
                        }
                    }
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

    void BuildBlock(int voxelType, int x, int y, int z, List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 scale)
    {
        //Left side
        if (IsFaceVisible(x - 1, y, z))
            BuildFace(voxelType, new Vector3(x, y, z), Vector3.up, Vector3.forward, false, verts, uvs, tris, scale);
        //Right side
        if (IsFaceVisible(x + 1, y, z))
            BuildFace(voxelType, new Vector3(x + 1, y, z), Vector3.up, Vector3.forward, true, verts, uvs, tris, scale);

        //Bottom side
        if (IsFaceVisible(x, y - 1, z))
            BuildFace(voxelType, new Vector3(x, y, z), Vector3.forward, Vector3.right, false, verts, uvs, tris, scale);
        //Top side
        if (IsFaceVisible(x, y + 1, z))
            BuildFace(voxelType, new Vector3(x, y + 1, z), Vector3.forward, Vector3.right, true, verts, uvs, tris, scale);

        //Back side
        if (IsFaceVisible(x, y, z - 1))
            BuildFace(voxelType, new Vector3(x, y, z), Vector3.up, Vector3.right, true, verts, uvs, tris, scale);
        //Front side
        if (IsFaceVisible(x, y, z + 1))
            BuildFace(voxelType, new Vector3(x, y, z + 1), Vector3.up, Vector3.right, false, verts, uvs, tris, scale);
    }

    void BuildFace(int voxelType, Vector3 corner, Vector3 up, Vector3 right, bool reversed, List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 scale)
    {
        //Uvs
        float offset = 0.02f;

        Vector2 uvWidth = new Vector2(0.25f - offset * 2, 0.25f - offset * 2);
        Vector2 uvCorner = new Vector2(0f + offset, 0.75f + offset);

        uvCorner.x = 0.25f * voxelType + offset;

        float uvRow = (corner.y + up.y) % 7;
        if (uvRow > 4)
            uvRow = 7 - uvRow;

        uvRow /= 4f;

        uvCorner.y = uvRow;

        uvs.Add(uvCorner);
        uvs.Add(new Vector2(uvCorner.x, uvCorner.y + uvWidth.y));
        uvs.Add(new Vector2(uvCorner.x + uvWidth.x, uvCorner.y + uvWidth.y));
        uvs.Add(new Vector2(uvCorner.x + uvWidth.x, uvCorner.y));

        //Vertices
        int index = verts.Count;

        //corner.x *= scale.x;
        //corner.y *= scale.y;
        //corner.z *= scale.z;

        up.x *= scale.x;
        up.y *= scale.y;
        up.z *= scale.z;

        right.x *= scale.x;
        right.y *= scale.y;
        right.z *= scale.z;

        verts.Add(corner);
        verts.Add(corner + up);
        verts.Add(corner + up + right);
        verts.Add(corner + right);

        //Triangles
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
        if (neighbourY < filterLayerMin || neighbourY > filterLayerMax)
            return true;

        if (GetVoxelType(neighbourX, neighbourY, neighbourZ) != -1)
            return false;

        return true;
    }

    int GetVoxelType(int x, int y, int z)
    {
        if ((x < 0) || (y < 0) || (z < 0) || (x >= terrainWidth * subBlocksPerParent.x) || (y >= terrainHeight * subBlocksPerParent.y) || (z >= terrainDepth * subBlocksPerParent.z))
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
