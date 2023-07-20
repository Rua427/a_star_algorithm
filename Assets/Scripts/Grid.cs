using System;
using UnityEngine;
using System.Collections.Generic;
public class Grid : MonoBehaviour
{

    //public bool onlyDisplayPathGizmos;
    public bool displayGridGizmos;
    public LayerMask unwalkableMask;
    public Vector2 gridWorldSize;
    public float nodeRadius;
    public TerrainType[] walkableRegions;
    public int obstacleProximityPenalty = 10;
    LayerMask walkableMask;
    Dictionary<int, int> walkableRegionDict = new Dictionary<int, int>();
    Node[,] grid;

    float nodeDiameter;
    int gridSizeX, gridSizeY;

    int penaltyMin = int.MaxValue;
    int penaltyMax = int.MinValue;

    private void Awake()
    {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);

        // layermask는 기본적으로 2진수 연산이다..
        // layer 2진수 값을 더하면 더한 layer만을 사용할 수 있음.
        // 비트 계산
        foreach (TerrainType region in walkableRegions)
        {
            // 2진수 합산 비트연산으로 ~
            walkableMask.value |= walkableMask.value | region.terrainMask.value;

            // 밑을 2로 하여 로그 계산시 10진수로 변환 가능
            int log = (int)Mathf.Log(region.terrainMask.value, 2);
            walkableRegionDict.Add(log, region.terrainPenalty);
        }

        // path finding 하기 위한 grid 생성
        CreateGrid();
    }

    public int MaxSize
    {
        get
        {
            return gridSizeX * gridSizeY;
        }
    }
    private void CreateGrid()
    {
        // 맵 크기만큼 격자 생성
        grid = new Node[gridSizeX, gridSizeY];

        // 맵의 좌 하단 좌표값
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.forward * gridWorldSize.y / 2;
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                // 각 격자의 위치 설정
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (y * nodeDiameter + nodeRadius);
                // 격자 주위 layer 확인하여 path finding 가능한 격자인지 확인
                bool walkable = !(Physics.CheckSphere(worldPoint, nodeRadius, unwalkableMask));
                int movementPenalty = 0;

                // raycast
                if (walkable)
                {
                    Ray ray = new Ray(worldPoint + Vector3.up * 50, Vector3.down);
                    RaycastHit hit;
                    // 그래서 설정한 모든 layer를 감지 할 수 있음!
                    if (Physics.Raycast(ray, out hit, 100, walkableMask))
                    {
                        walkableRegionDict.TryGetValue(hit.collider.gameObject.layer, out movementPenalty);
                    }
                }

                // 장애물에는 가장 큰 값을 주어서 길 > 장애물 이 되지 않도록 함.
                if (!walkable)
                {
                    movementPenalty += obstacleProximityPenalty;
                }
                // 격자 최종 생성
                grid[x, y] = new Node(walkable, worldPoint, x, y, movementPenalty);
            }
        }

        BlurPenaltyMap(3);
    }

    // 각 grid 패널티 값을 blur처럼 적용하여 path line 을 자연스럽게 조정
    void BlurPenaltyMap(int blurSize)
    {

        // 블러 사이즈
        int kernelSize = blurSize * 2 + 1;
        int kernelExtents = (kernelSize - 1) / 2;

        int[,] penaltiesHorizontalPass = new int[gridSizeX, gridSizeY];
        int[,] penaltiesVerticalPass = new int[gridSizeX, gridSizeY];

        for (int y = 0; y < gridSizeY; y++)
        {
            // 첫번째 열
            for (int x = -kernelExtents; x <= kernelExtents; x++)
            {
                int sampleX = Mathf.Clamp(x, 0, kernelExtents);
                penaltiesHorizontalPass[0, y] += grid[sampleX, y].movementPenalty;
            }

            // 행 grid만 계산
            for (int x = 1; x < gridSizeX; x++)
            {
                int removeIndex = Mathf.Clamp(x - kernelExtents - 1, 0, gridSizeX);
                int addIndex = Mathf.Clamp(x + kernelExtents, 0, gridSizeX - 1);

                penaltiesHorizontalPass[x, y] = penaltiesHorizontalPass[x - 1, y] - grid[removeIndex, y].movementPenalty + grid[addIndex, y].movementPenalty;
            }
        }

        for (int x = 0; x < gridSizeX; x++)
        {
            // 첫번째 행
            for (int y = -kernelExtents; y <= kernelExtents; y++)
            {
                int sampleY = Mathf.Clamp(y, 0, kernelExtents);
                penaltiesVerticalPass[x, 0] += penaltiesHorizontalPass[x, sampleY];
            }

            int blurredPenalty = Mathf.RoundToInt((float)penaltiesVerticalPass[x, 0] / (kernelSize * kernelSize));
            grid[x, 0].movementPenalty = blurredPenalty;

            // 열 grid 계산
            for (int y = 1; y < gridSizeY; y++)
            {
                int removeIndex = Mathf.Clamp(y - kernelExtents - 1, 0, gridSizeY);
                int addIndex = Mathf.Clamp(y + kernelExtents, 0, gridSizeY - 1);

                penaltiesVerticalPass[x, y] = penaltiesVerticalPass[x, y - 1] - penaltiesHorizontalPass[x, removeIndex] + penaltiesHorizontalPass[x, addIndex];

                blurredPenalty = Mathf.RoundToInt((float)penaltiesVerticalPass[x, y] / (kernelSize * kernelSize));
                grid[x, y].movementPenalty = blurredPenalty;

                if (blurredPenalty > penaltyMax)
                {
                    penaltyMax = blurredPenalty;
                }
                if (blurredPenalty < penaltyMin)
                {
                    penaltyMin = blurredPenalty;
                }
            }
        }
    }
    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) { continue; }

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    neighbours.Add(grid[checkX, checkY]);
                }
            }
        }

        return neighbours;
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = ((worldPosition.x - transform.position.x) + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = ((worldPosition.z - transform.position.z) + gridWorldSize.y / 2) / gridWorldSize.y;

        Debug.Log("PercentX: " + percentX);
        Debug.Log("PercentY: " + percentY);

        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);

        return grid[x, y];
    }
    // public List<Node> path;
    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, 1, gridWorldSize.y));

        // if (onlyDisplayPathGizmos)
        // {
        //     if (path != null)
        //     {
        //         foreach (Node n in path)
        //         {
        //             Gizmos.color = Color.black;
        //             Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - .1f));
        //         }
        //     }
        // }
        // else
        // {
        if (grid != null && displayGridGizmos)
        {
            foreach (Node n in grid)
            {

                Gizmos.color = Color.Lerp(Color.white, Color.black, Mathf.InverseLerp(penaltyMin, penaltyMax, n.movementPenalty));
                Gizmos.color = (n.walkable) ? Gizmos.color : Color.red;

                // if (path != null)
                // {
                //     if (path.Contains(n))
                //     {
                //         Gizmos.color = Color.black;
                //     }
                // }
                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter));
            }
        }
        // }

    }

    // 각 지형별 penalty값을 주어 path finding 우선 순위를 결정 할 수 있게 함.
    [System.Serializable]
    public class TerrainType
    {
        public LayerMask terrainMask;
        public int terrainPenalty;
    }
}