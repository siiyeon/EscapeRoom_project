using UnityEngine;

namespace PrologueCutscene
{
// 산길 도로 중앙선 경로 (블렌더 도로 모델과 같은 제어점). 차량 주행과 배치에 사용.
public class RoadPath : MonoBehaviour
{
    public Vector3[] points = {
        new Vector3(0,0,-40), new Vector3(0,0,20), new Vector3(10,0,70), new Vector3(25,0,120),
        new Vector3(20,0,170), new Vector3(5,0,220), new Vector3(0,0,260), new Vector3(0,0,310)
    };
    public int samplesPerSegment = 40;
    Vector3[] cache;

    static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    public Vector3[] Sampled()
    {
        if (cache != null && cache.Length > 0) return cache;
        int n = points.Length;
        var list = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, n - 1)];
            for (int s = 0; s < samplesPerSegment; s++) list.Add(CR(p0, p1, p2, p3, s / (float)samplesPerSegment));
        }
        list.Add(points[n - 1]);
        cache = list.ToArray();
        return cache;
    }

    // z 값(앞으로 간 거리)에 해당하는 도로 중앙 위치와 진행 방향
    public void SampleAtZ(float z, out Vector3 pos, out Vector3 fwd)
    {
        var s = Sampled();
        for (int i = 0; i < s.Length - 1; i++)
        {
            if (s[i + 1].z >= z)
            {
                float t = Mathf.InverseLerp(s[i].z, s[i + 1].z, z);
                pos = transform.TransformPoint(Vector3.Lerp(s[i], s[i + 1], t));
                fwd = transform.TransformDirection((s[i + 1] - s[i]).normalized);
                return;
            }
        }
        pos = transform.TransformPoint(s[s.Length - 1]);
        fwd = transform.TransformDirection((s[s.Length - 1] - s[s.Length - 2]).normalized);
    }

    void OnDrawGizmos()
    {
        cache = null;
        var s = Sampled();
        Gizmos.color = Color.yellow;
        for (int i = 0; i < s.Length - 1; i++) Gizmos.DrawLine(transform.TransformPoint(s[i]), transform.TransformPoint(s[i + 1]));
    }
}
}
