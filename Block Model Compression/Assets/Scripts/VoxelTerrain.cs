using System.Collections;
using System.Collections.Generic;
using UnityEditor;
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
    public int[,,] voxelTypes;
    public int[,,] parentBlocks;
    public List<SubBlock> subBlocks = new List<SubBlock>();

    public Mesh mesh;
    public MeshRenderer meshRenderer;
    public MeshFilter meshFilter;

    public int solidTerrainLevel;
    public VoxelSphere[] spheres;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();

        voxelProbabilities = new VoxelTypeProbability[terrainWidth * subBlocksPerParent.x, terrainHeight * subBlocksPerParent.y, terrainDepth * subBlocksPerParent.z];

        InitialiseProbabilities();
        GeneratePerlinTerrain();
        GenerateProbabilitiesSphere();

        CreateBlockModel();

        int voxelCount = terrainWidth * terrainHeight * terrainDepth * subBlocksPerParent.x * subBlocksPerParent.y * subBlocksPerParent.z;

        Debug.Log("Number of voxels: " + voxelCount +  ", Number of blocks: " + subBlocks.Count);

        GenerateMesh();
    }

    public void Regenerate()
    {
        Start();
    }

    void InitialiseProbabilities()
    {
        for (int x = 0; x < terrainWidth * subBlocksPerParent.x; x++)
        {
            for (int y = 0; y < terrainHeight * subBlocksPerParent.y; y++)
            {
                for (int z = 0; z < terrainDepth * subBlocksPerParent.z; z++)
                {
                    VoxelTypeProbability probability = new VoxelTypeProbability();
                    probability.Probabilities = new float[numVoxelTypes];

                    for (int i = 0; i < numVoxelTypes; i++)
                        probability.Probabilities[i] = 0f;

                    voxelProbabilities[x, y, z] = probability;
                }
            }
        }
    }

    void GeneratePerlinTerrain()
    {
        for (int x = 0; x < terrainWidth * subBlocksPerParent.x; x++)
        {
            for (int z = 0; z < terrainDepth * subBlocksPerParent.z; z++)
            {
                float noise = Mathf.PerlinNoise(x / (float)(terrainWidth * subBlocksPerParent.x), z / (float)(terrainDepth * subBlocksPerParent.z));

                int nonSolidTerrainHeight = (terrainHeight * subBlocksPerParent.y) - solidTerrainLevel;
                int columnHeight = solidTerrainLevel + (int)(nonSolidTerrainHeight * noise);

                for (int y = 0; y < terrainHeight * subBlocksPerParent.y; y++)
                {
                    VoxelTypeProbability probability = voxelProbabilities[x, y, z];

                    if (y < columnHeight)
                    {
                        probability.Probabilities[0] = 1f;
                    }

                    voxelProbabilities[x, y, z] = probability;
                }
            }
        }
    }

    void GenerateProbabilitiesSphere()
    {
        foreach (VoxelSphere sphere in spheres)
        {
            float sphereRadiusSquared = sphere.radius * sphere.radius;

            for (int x = 0; x < terrainWidth * subBlocksPerParent.x; x++)
            {
                for (int y = 0; y < terrainHeight * subBlocksPerParent.y; y++)
                {
                    for (int z = 0; z < terrainDepth * subBlocksPerParent.z; z++)
                    {
                        VoxelTypeProbability probability = voxelProbabilities[x, y, z];

                        Vector3 voxelPosition = new Vector3(x, y, z);
                        float squareDistance = (sphere.center - voxelPosition).sqrMagnitude;

                        if (squareDistance <= sphereRadiusSquared)
                        {
                            for (int i = 0; i < numVoxelTypes; i++)
                                probability.Probabilities[i] = 0.01f;

                            probability.Probabilities[sphere.voxelType] = 1f;
                        }

                        voxelProbabilities[x, y, z] = probability;
                    }
                }
            }
        }
    }

    public void CreateBlockModel()
    {
        parentBlocks = new int[terrainWidth, terrainHeight, terrainDepth];
        voxelTypes = new int[terrainWidth * subBlocksPerParent.x, terrainHeight * subBlocksPerParent.y, terrainDepth * subBlocksPerParent.z];

        for (int x = 0; x < terrainWidth * subBlocksPerParent.x; x++)
        {
            for (int y = 0; y < terrainHeight * subBlocksPerParent.y; y++)
            {
                for (int z = 0; z < terrainDepth * subBlocksPerParent.z; z++)
                {
                    voxelTypes[x, y, z] = GetVoxelType(x, y, z);
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

                    subBlocks.AddRange(CalculateSubBlocks(x, y, z));
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

        Vector3 offset = new Vector3(
            parentBlockSize.x / subBlocksPerParent.x,
            parentBlockSize.y / subBlocksPerParent.y,
            parentBlockSize.z / subBlocksPerParent.z);

        offset /= 2f;

        foreach (SubBlock subBlock in subBlocks)
        {
            Vector3 scale = new Vector3(subBlock.Size.x / subBlocksPerParent.x, subBlock.Size.y / subBlocksPerParent.y, subBlock.Size.z / subBlocksPerParent.z);
            Vector3 subBlockOrigin = subBlock.OriginWorld - subBlock.SizeWorld + 2 * offset;


            int minY = subBlock.Origin.y;
            int maxY = subBlock.Origin.y + subBlock.Size.y;

            if (filterLayerMin > maxY || filterLayerMax < minY)
                continue;

            BuildBlock(subBlock.VoxelType, subBlockOrigin.x, subBlockOrigin.y, subBlockOrigin.z, verts, uvs, tris, subBlock.SizeWorld);
        }


        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
    }

    void BuildBlock(int voxelType, float x, float y, float z, List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 scale)
    {
        //Left side
        if (IsFaceVisible(x / parentBlockSize.x - 1, y / parentBlockSize.y, z / parentBlockSize.z))
            BuildFace(voxelType, new Vector3(x, y, z), Vector3.up, Vector3.forward, false, verts, uvs, tris, scale);
        //Right side
        if (IsFaceVisible(x / parentBlockSize.x + 1, y / parentBlockSize.y, z / parentBlockSize.z))
            BuildFace(voxelType, new Vector3(x + scale.x, y, z), Vector3.up, Vector3.forward, true, verts, uvs, tris, scale);

        //Bottom side
        if (IsFaceVisible(x / parentBlockSize.x, y / parentBlockSize.y - 1, z / parentBlockSize.z))
            BuildFace(voxelType, new Vector3(x, y, z), Vector3.forward, Vector3.right, false, verts, uvs, tris, scale);
        //Top side
        if (IsFaceVisible(x / parentBlockSize.x, y / parentBlockSize.y + 1, z / parentBlockSize.z))
            BuildFace(voxelType, new Vector3(x, y + scale.y, z), Vector3.forward, Vector3.right, true, verts, uvs, tris, scale);

        //Back side
        if (IsFaceVisible(x / parentBlockSize.x, y / parentBlockSize.y, z / parentBlockSize.z - 1))
            BuildFace(voxelType, new Vector3(x, y, z), Vector3.up, Vector3.right, true, verts, uvs, tris, scale);
        //Front side
        if (IsFaceVisible(x / parentBlockSize.x, y / parentBlockSize.y, z / parentBlockSize.z + 1))
            BuildFace(voxelType, new Vector3(x, y, z + scale.z), Vector3.up, Vector3.right, false, verts, uvs, tris, scale);
    }

    void BuildFace(int voxelType, Vector3 corner, Vector3 up, Vector3 right, bool reversed, List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 scale)
    {
        if (voxelType < 0 || voxelType >= 3)
            return;

        //Uvs
        float offset = 0.02f;

        Vector2 uvWidth = new Vector2(0.25f - offset * 2, 0.25f - offset * 2);
        Vector2 uvCorner = new Vector2(0f + offset, 0.75f + offset);

        uvCorner.x = 0.25f * voxelType + offset;

        float uvRow = ((int)corner.y + (int)up.y) % 7;
        if (uvRow >= 4)
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

    bool IsFaceVisible(float neighbourX, float neighbourY, float neighbourZ)
    {

        if (neighbourY < filterLayerMin / subBlocksPerParent.y || neighbourY >= filterLayerMax / subBlocksPerParent.y)
            return true;

        if ((neighbourX < 0) || (neighbourY < 0) || (neighbourZ < 0) || (neighbourX >= terrainWidth) || (neighbourY >= terrainHeight) || (neighbourZ >= terrainDepth))
            return true;

        if (parentBlocks[(int)neighbourX, (int)neighbourY, (int)neighbourZ] == -2)
            return true;

        if (parentBlocks[(int)neighbourX, (int)neighbourY, (int)neighbourZ] != -1)
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

    int[,,] rCumulativeSum;
    int[,,] cCumulativeSum;
    int[,,] finalCumulativeSum;

    int[,,] debugValues;
    int[,,] finalDebugValues;

    public bool debugUseFinalSum = true;

    Vector3 maxOriginWorld;
    Vector3 maxSizeWorld;


    List<Vector3> maxOriginWorldList = new List<Vector3>();
    List<Vector3> maxSizeWorldList = new List<Vector3>();

    Vector3Int maxOrigin;
    Vector3Int maxSize;

    public int maxSum = -1;
    public int bestK = 0;
    public int bestM = 0;

    bool calcuated = false;

    List<SubBlock> CalculateSubBlocks(int parentX, int parentY, int parentZ)
    {
        List<SubBlock> currentSubBlocks = new List<SubBlock>();

        for (int voxelType = 0; voxelType < numVoxelTypes; voxelType++)
        {
            bool subBlockAdded = true;

            while (subBlockAdded)
            {
                rCumulativeSum = new int[subBlocksPerParent.x, subBlocksPerParent.y, subBlocksPerParent.z];

                maxSum = 0;
                bestK = 0;
                bestM = 0;

                maxSize = Vector3Int.zero;

                int maxDimension = Mathf.Max(subBlocksPerParent.x, subBlocksPerParent.y);
                maxDimension = Mathf.Max(maxDimension, subBlocksPerParent.z);

                for (int K = 1; K <= maxDimension; K++)
                {
                    for (int M = 1; M <= maxDimension; M++)
                    {
                        for (int x = 0; x < subBlocksPerParent.x; x++)
                        {
                            for (int y = 0; y < subBlocksPerParent.y; y++)
                            {
                                int currentRSum = 0;

                                for (int z = 0; z < subBlocksPerParent.z; z++)
                                {
                                    int voxelX = x + parentX * subBlocksPerParent.x;
                                    int voxelY = y + parentY * subBlocksPerParent.y;
                                    int voxelZ = z + parentZ * subBlocksPerParent.z;

                                    if (voxelTypes[voxelX, voxelY, voxelZ] == voxelType)
                                        currentRSum++;
                                    else
                                        currentRSum = 0;

                                    rCumulativeSum[x, y, z] = currentRSum;
                                }
                            }
                        }

                        cCumulativeSum = new int[subBlocksPerParent.x, subBlocksPerParent.y, subBlocksPerParent.z];

                        //int K = 2;

                        for (int x = 0; x < subBlocksPerParent.x; x++)
                        {
                            for (int z = 0; z < subBlocksPerParent.z; z++)
                            {
                                int currentCSum = 0;

                                for (int y = 0; y < subBlocksPerParent.y; y++)
                                {
                                    if (rCumulativeSum[x, y, z] >= K)
                                        currentCSum++;
                                    else
                                        currentCSum = 0;

                                    cCumulativeSum[x, y, z] = currentCSum;
                                }
                            }
                        }

                        finalCumulativeSum = new int[subBlocksPerParent.x, subBlocksPerParent.y, subBlocksPerParent.z];
                        debugValues = new int[subBlocksPerParent.x, subBlocksPerParent.y, subBlocksPerParent.z];

                        //int M = 2;

                        for (int y = 0; y < subBlocksPerParent.y; y++)
                        {
                            for (int z = 0; z < subBlocksPerParent.z; z++)
                            {
                                int currentFinalSum = 0;

                                for (int x = 0; x < subBlocksPerParent.x; x++)
                                {
                                    if (cCumulativeSum[x, y, z] >= M)
                                        currentFinalSum++;
                                    else
                                        currentFinalSum = 0;

                                    int voxelX = x + parentX * subBlocksPerParent.x;
                                    int voxelY = y + parentY * subBlocksPerParent.y;
                                    int voxelZ = z + parentZ * subBlocksPerParent.z;

                                    finalCumulativeSum[x, y, z] = currentFinalSum;
                                    debugValues[x, y, z] = currentFinalSum * K * M;

                                    if (finalCumulativeSum[x, y, z] * K * M > maxSum)
                                    {
                                        finalDebugValues = debugValues;

                                        maxSum = finalCumulativeSum[x, y, z] * K * M;
                                        bestK = K;
                                        bestM = M;

                                        maxOriginWorld = new Vector3(
                                            voxelX * parentBlockSize.x / subBlocksPerParent.x,
                                            voxelY * parentBlockSize.y / subBlocksPerParent.y,
                                            voxelZ * parentBlockSize.z / subBlocksPerParent.z);


                                        maxOrigin = new Vector3Int(voxelX, voxelY, voxelZ);

                                        maxSizeWorld = new Vector3(
                                           finalCumulativeSum[x, y, z] * parentBlockSize.x / subBlocksPerParent.x,
                                           M * parentBlockSize.y / subBlocksPerParent.y,
                                           K * parentBlockSize.z / subBlocksPerParent.z);

                                        maxSize = new Vector3Int(finalCumulativeSum[x, y, z], M, K);
                                    }
                                }
                            }
                        }
                    }
                }

                if (maxSize != Vector3Int.zero)
                {
                    if (maxOriginWorldList.Count == 0)
                    {
                        maxOriginWorldList.Add(maxOriginWorld);
                        maxSizeWorldList.Add(maxSizeWorld);
                    }

                    currentSubBlocks.Add(new SubBlock()
                    {
                        OriginWorld = maxOriginWorld,
                        SizeWorld = maxSizeWorld,
                        Origin = maxOrigin,
                        Size = maxSize,
                        VoxelType = voxelType
                    });

                    //Remove cuboid from array
                    for (int x = 0; x < maxSize.x; x++)
                    {
                        for (int y = 0; y < maxSize.y; y++)
                        {
                            for (int z = 0; z < maxSize.z; z++)
                            {
                                voxelTypes[maxOrigin.x - x, maxOrigin.y - y, maxOrigin.z - z] = -1;
                            }
                        }
                    }
                }
                else
                {
                    subBlockAdded = false;
                }
            }
        }

        calcuated = true;

        return currentSubBlocks;
    }

    void OnDrawGizmos()
    {
        if (!calcuated)
            return;

        Vector3 offset = new Vector3(
            parentBlockSize.x / subBlocksPerParent.x,
            parentBlockSize.y / subBlocksPerParent.y,
            parentBlockSize.z / subBlocksPerParent.z);

        offset /= 2f;

        for (int i = 0; i < maxOriginWorldList.Count; i++)
        {
            maxOriginWorld = maxOriginWorldList[i];
            maxSizeWorld = maxSizeWorldList[i];


            Handles.color = Color.yellow;
            Handles.DrawWireCube((maxOriginWorld + 2 * offset + maxOriginWorld - maxSizeWorld + 2 * offset) / 2f, maxSizeWorld);

            Handles.DrawSphere(0, maxOriginWorld + 2 * offset, Quaternion.identity, 0.025f);

            Handles.color = Color.green;
            Handles.DrawSphere(0, maxOriginWorld - maxSizeWorld + 2 * offset, Quaternion.identity, 0.025f);
            Handles.color = Color.white;
        }

        for (int x = 0; x < subBlocksPerParent.x; x++)
        {
            for (int y = 0; y < subBlocksPerParent.y; y++)
            {
                for (int z = 0; z < subBlocksPerParent.z; z++)
                {
                    Vector3 origin = new Vector3(
                        x * parentBlockSize.x / subBlocksPerParent.x,
                        y * parentBlockSize.y / subBlocksPerParent.y,
                        z * parentBlockSize.z / subBlocksPerParent.z);

                    GUIStyle style = new GUIStyle();

                    if (debugUseFinalSum)
                    {
                        if (finalDebugValues[x, y, z] == 0)
                            style.normal.textColor = Color.grey;
                        else
                            style.normal.textColor = Color.red;

                        Handles.Label(origin + offset, finalDebugValues[x, y, z].ToString(), style);
                    }
                    else
                    {
                        if (rCumulativeSum[x, y, z] == 0)
                            style.normal.textColor = Color.grey;
                        else
                            style.normal.textColor = Color.red;

                        Handles.Label(origin + offset, rCumulativeSum[x, y, z].ToString(), style);
                    }
                }
            }
        }


    }
}

[System.Serializable]
public class VoxelSphere
{
    public float radius;
    public Vector3 center;
    public int voxelType;
}
