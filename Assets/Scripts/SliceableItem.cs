using System.Collections.Generic;
using UnityEngine;

public class SliceableItem : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private float forceValue = 4f;

    public void SliceIntoPieces(GameObject obj, int targetPieces)
    {
        var results = new List<GameObject>();
        Queue<(GameObject piece, int required)> queue = new();
    
        queue.Enqueue((obj, targetPieces));
    
        while (queue.Count > 0)
        {
            var (piece, required) = queue.Dequeue();
    
            if (required <= 1)
            {
                results.Add(piece);
                continue;
            }
    
            var newPieces = Slice(piece);
    
            if (newPieces is {Length: 2})
            {
                var leftTarget = required / 2;
                var rightTarget = required - leftTarget;
    
                queue.Enqueue((newPieces[0], leftTarget));
                queue.Enqueue((newPieces[1], rightTarget));
            }
            else
            {
                results.Add(piece);
            }
        }
    
        Debug.Log("Got " + results.Count + " pieces");
    }
    
    private static Plane GenerateRandomPlane(GameObject obj)
    {
        var bounds = obj.GetComponent<MeshRenderer>().bounds;

        var point = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            Random.Range(bounds.min.z, bounds.max.z)
        );

        var normal = Random.onUnitSphere;

        return new Plane(normal, point);
    }

    private GameObject[] Slice(GameObject obj)
    {
        if (meshFilter == null) return null;
        
        var positiveVertices = new List<Vector3>();
        var negativeVertices = new List<Vector3>();
        
        var positiveTriangles = new List<int>();
        var negativeTriangles = new List<int>();

        var mesh = meshFilter.mesh;
        var vertices = mesh.vertices;
        var triangles = mesh.triangles;
        
        var plane = GenerateRandomPlane(obj);

        for (int i = 0; i < triangles.Length; i += 3)
        {
            var v0 = obj.transform.TransformPoint(vertices[triangles[i]]);
            var v1 = obj.transform.TransformPoint(vertices[triangles[i+1]]);
            var v2 = obj.transform.TransformPoint(vertices[triangles[i+2]]);

            var d0 = plane.GetDistanceToPoint(v0);
            var d1 = plane.GetDistanceToPoint(v1);
            var d2 = plane.GetDistanceToPoint(v2);
           
            
            if (d0 <= 0 && d1 <= 0 && d2 <= 0) {
                AddTriangle(negativeVertices, negativeTriangles, v0, v1, v2, obj.transform);
            }
            else if (d0 >= 0 && d1 >= 0 && d2 >= 0) {
                AddTriangle(positiveVertices, positiveTriangles, v0, v1, v2, obj.transform);
            }
            else {
                SplitTriangle(v0, v1, v2, d0, d1, d2,
                    positiveVertices, positiveTriangles,
                    negativeVertices, negativeTriangles,
                    plane, obj.transform);
            }
        }

        var posObj = CreateMeshObject(obj, positiveVertices, positiveTriangles, obj.name + "_pos");
        var negObj = CreateMeshObject(obj, negativeVertices, negativeTriangles, obj.name + "_neg");
        var rbPos = posObj.GetComponent<Rigidbody>();
        var rbNeg = negObj.GetComponent<Rigidbody>();

        var randomDir = plane.normal;
        rbPos.AddForce(randomDir * forceValue, ForceMode.Impulse);
        rbNeg.AddForce(-randomDir * forceValue, ForceMode.Impulse);

        Destroy(obj);

        return new[] { posObj, negObj };
    }
    
    private void SplitTriangle(Vector3 v0, Vector3 v1, Vector3 v2, float d0, float d1, float d2,
        List<Vector3> posVerts, List<int> posTris, List<Vector3> negVerts, List<int> negTris, Plane plane, Transform parent)
    {
        Vector3[] verts = { v0, v1, v2 };
        float[] d = { d0, d1, d2 };

        var posSide = new List<Vector3>();
        var negSide = new List<Vector3>();

        for (int i = 0; i < 3; i++)
        {
            if (d[i] >= 0)
                posSide.Add(verts[i]);
            else
                negSide.Add(verts[i]);
        }

        if (posSide.Count == 2 && negSide.Count == 1)
        {
            var i0 = GetLinePlaneIntersection(posSide[0], negSide[0], plane);
            var i1 = GetLinePlaneIntersection(posSide[1], negSide[0], plane);

            AddTriangle(posVerts, posTris, posSide[0], posSide[1], i0, parent);
            AddTriangle(posVerts, posTris, posSide[1], i1, i0, parent);
            AddTriangle(negVerts, negTris, negSide[0], i0, i1, parent);
        }
        else if (negSide.Count == 2 && posSide.Count == 1)
        {
            var i0 = GetLinePlaneIntersection(negSide[0], posSide[0], plane);
            var i1 = GetLinePlaneIntersection(negSide[1], posSide[0], plane);

            AddTriangle(negVerts, negTris, negSide[0], negSide[1], i0, parent);
            AddTriangle(negVerts, negTris, negSide[1], i1, i0, parent);

            AddTriangle(posVerts, posTris, posSide[0], i0, i1, parent);
        }
        else
        {
            AddTriangle(posVerts, posTris, v0, v1, v2, parent);
        }
    }
    private Vector3 GetLinePlaneIntersection(Vector3 a, Vector3 b, Plane plane)
    {
        var ab = b - a;
        var t = (-plane.GetDistanceToPoint(a)) / Vector3.Dot(plane.normal, ab);
        return a + ab * t;
    }


    private void AddTriangle(List<Vector3> verts, List<int> tris, Vector3 v0, Vector3 v1, Vector3 v2, Transform parent)
    {
        var baseIndex = verts.Count;
        verts.Add(parent.InverseTransformPoint(v0));
        verts.Add(parent.InverseTransformPoint(v1));
        verts.Add(parent.InverseTransformPoint(v2));
        
        tris.Add(baseIndex);
        tris.Add(baseIndex + 1);
        tris.Add(baseIndex + 2);
    }
    private GameObject CreateMeshObject(GameObject original, List<Vector3> verts, List<int> tris, string name)
    {
        GameObject obj = new GameObject(name)
        {
            transform =
            {
                position = original.transform.position,
                rotation = original.transform.rotation,
                localScale = original.transform.localScale
            }
        };

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);

        mesh.RecalculateNormals();   
        mesh.RecalculateBounds();    

        var newObjectMesh = obj.AddComponent<MeshFilter>();
        newObjectMesh.mesh = mesh;

        var mr = obj.AddComponent<MeshRenderer>();
        mr.materials = original.GetComponent<MeshRenderer>().materials;

        AddPhysics(obj);

        return obj;
    }

    private static void AddPhysics(GameObject obj)
    {
        if (obj.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = obj.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (obj.GetComponent<Collider>() == null)
        {
            MeshCollider mc = obj.AddComponent<MeshCollider>();
            mc.convex = true;
        }
    }
}
