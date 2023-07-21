using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System;



public class Pathfinding : MonoBehaviour
{
    // PathRequestManager requestManager;

    Grid grid;

    private void Awake()
    {
        grid = GetComponent<Grid>();
        // requestManager = GetComponent<PathRequestManager>();
    }

    private void Update()
    {
        // if (Input.GetButtonDown("Jump")){
        //     FindPath(seeker.position, target.position);
        // }
    }

    // public void StartFindPath(Vector3 startPos, Vector3 targetPos)
    // {
    //     StartCoroutine(FindPath(startPos, targetPos));
    // }
    public void FindPath(PathRequest request, Action<PathResult> callback)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        Vector3[] wayPoints = new Vector3[0];
        bool pathSuccess = false;

        // position에 따른 grid selecting
        Node startNode = grid.NodeFromWorldPoint(request.pathStart);
        Node targetNode = grid.NodeFromWorldPoint(request.pathEnd);


        // 해당 노드가 탐색 가능한 곳일경우에만
        if (startNode.walkable && targetNode.walkable)
        {

            // heap 정렬로 성능 최적화
            Heap<Node> openSet = new Heap<Node>(grid.MaxSize);
            HashSet<Node> closedSet = new HashSet<Node>();

            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
                Node currentNode = openSet.RemoveFirst();
                closedSet.Add(currentNode);

                if (currentNode == targetNode)
                {
                    sw.Stop();
                    print("Path found:" + sw.ElapsedMilliseconds + "ms");
                    pathSuccess = true;
                    break;
                }

                foreach (Node neighbour in grid.GetNeighbours(currentNode))
                {
                    if (!neighbour.walkable || closedSet.Contains(neighbour))
                    {
                        continue;
                    }

                    //현재 node의 총 비용 + 현재 node와 이웃 node 사이의 비용. (start지점에서 현재 node까지의 거리 비용)
                    int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour) + neighbour.movementPenalty;

                    if (newMovementCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                    {
                        neighbour.gCost = newMovementCostToNeighbour;
                        neighbour.hCost = GetDistance(neighbour, targetNode);

                        neighbour.parent = currentNode;

                        if (!openSet.Contains(neighbour))
                        {
                            openSet.Add(neighbour);
                        }
                        else
                        {
                            openSet.UpdateItem(neighbour);
                        }
                    }
                }
            }
        }


        if (pathSuccess)
        {
            wayPoints = RetracePath(startNode, targetNode);
            pathSuccess = wayPoints.Length > 0;
        }

        callback(new PathResult(wayPoints, pathSuccess, request.callback));
        // requestManager.FinishedProcessingPath(wayPoints, pathSuccess);
    }

    // 탐색이 모두 완료되어 찾은 path의 위치를 가져옴 world position
    Vector3[] RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();

        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        Vector3[] wayPoints = SimplyfyPath(path);
        Array.Reverse(wayPoints);
        return wayPoints;

    }

    // Node에서 위치값만 추출 Node → Vector3 
    Vector3[] SimplyfyPath(List<Node> path)
    {
        List<Vector3> wayPoints = new List<Vector3>();
        Vector2 directionOld = Vector2.zero;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2 directionNew = new Vector2(path[i - 1].gridX - path[i].gridX, path[i - 1].gridY - path[i].gridY);
            if (directionNew != directionOld)
            {
                wayPoints.Add(path[i].worldPosition);
            }
            directionOld = directionNew;
        }

        return wayPoints.ToArray();
    }
    int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        if (dstX > dstY)
        {
            return 14 * dstY + 10 * (dstX - dstY);
        }

        return 14 * dstX + 10 * (dstY - dstX);
    }
}
