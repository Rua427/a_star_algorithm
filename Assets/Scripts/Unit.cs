using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unit : MonoBehaviour
{
    const float minPathUpdateTime = .2f;
    const float pathUpdateMoveThreshold = .5f;
    public Transform target;
    public float speed = 20;
    public float turnDst = 5;
    public float turnSpeed = 5;
    public float stoppingDst = 10;
    Path path;
    //int targetIndex;

    void Start()
    {
        StartCoroutine(UpdatePath());
    }
    public void OnPathFound(Vector3[] wayPoints, bool pathSuccessful)
    {
        if (pathSuccessful)
        {
            path = new Path(wayPoints, transform.position, turnDst, stoppingDst);
            StopCoroutine("FollowPath");
            StartCoroutine("FollowPath");
        }
    }

    // 최초 실행시, map load 시간때문에 첫 프레임에 deltaTime값이 너무 높게 잡혀 
    // deltaTime값으로 움직이는 모든 오브젝트들이 비정상적으로 움직일 수 있음!
    // 
    IEnumerator UpdatePath()
    {
        // timeSinceLevelLoad map load되는 시간
        yield return new WaitForSeconds(Time.timeSinceLevelLoad);

        PathRequestManager.RequestPath(new PathRequest(transform.position, target.position, OnPathFound));
        float sqrMoveThreshold = pathUpdateMoveThreshold * pathUpdateMoveThreshold;
        Vector3 targetPosOld = target.position;

        while (true)
        {
            yield return new WaitForSeconds(minPathUpdateTime);
            if ((target.position - targetPosOld).sqrMagnitude > sqrMoveThreshold)
            {
                PathRequestManager.RequestPath(transform.position, target.position, OnPathFound);
                targetPosOld = target.position;
            }
        }
    }
    IEnumerator FollowPath()
    {
        //Vector3 currentWayPoint = path[0];
        bool followingPath = true;
        int pathIndex = 0;
        transform.LookAt(path.lookPoints[0]);

        float speedPercent = 1;

        while (followingPath)
        {
            Vector2 pos2D = new Vector2(transform.position.x, transform.position.z);

            // 속도가 너무 빠르면 한 프레임안에 여러 path를 건너뛸수 있음.
            // 현재 구조가 모든 path로 이동해서, 비정상 동작 가능성이 있음.
            // if (path.turnBoundaries[pathIndex].HasCrossedLine(pos2D))
            // {
            //     if (pathIndex == path.finishLineIndex)
            //     {
            //         followingPath = false;
            //     }
            //     else
            //     {
            //         pathIndex++;
            //     }
            // }
            while (path.turnBoundaries[pathIndex].HasCrossedLine(pos2D))
            {
                if (pathIndex == path.finishLineIndex)
                {
                    followingPath = false;
                    break;
                }
                else
                {
                    pathIndex++;
                }
            }


            if (followingPath)
            {
                if (pathIndex >= path.slowDownIndex && stoppingDst > 0)
                {
                    speedPercent = Mathf.Clamp01(path.turnBoundaries[path.finishLineIndex].DistanceFromPoint(pos2D) / stoppingDst);
                    if (speedPercent < 0.01f)
                    {
                        followingPath = false;
                    }
                }
                Quaternion targetRotation = Quaternion.LookRotation(path.lookPoints[pathIndex] - transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);

                // Space.World = 월드 축을 기준으로
                // Space.Self = 자기 자신 축을 기준으로
                transform.Translate(Vector3.forward * Time.deltaTime * speed * speedPercent, Space.Self);
            }
            // if (transform.position == currentWayPoint)
            // {
            //     targetIndex++;
            //     if (targetIndex >= path.Length)
            //     {
            //         yield break;
            //     }

            //     currentWayPoint = path[targetIndex];
            //     currentWayPoint.y = target.position.y;
            // }
            // transform.position = Vector3.MoveTowards(transform.position, currentWayPoint, speed);
            yield return null;
        }

    }

    public void OnDrawGizmos()
    {
        if (path != null)
        {
            // for (int i = targetIndex; i < path.Length; i++)
            // {
            //     Gizmos.color = Color.black;
            //     Gizmos.DrawCube(path[i], Vector3.one);

            //     if (i == targetIndex)
            //     {
            //         Gizmos.DrawLine(transform.position, path[i]);
            //     }
            //     else
            //     {
            //         Gizmos.DrawLine(path[i - 1], path[i]);
            //     }
            // }

            path.DrawWithGizmos();
        }
    }
}
