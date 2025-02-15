using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices; // for StructLayout if needed

[DefaultExecutionOrder(500)]
public class CollisionMeshAllBodies : MonoBehaviour
{
    // --------------------------------------------------------
    // Materials and Shaders
    // --------------------------------------------------------
    private Material lineMaterial;
    public ComputeShader transformShader;

    // --------------------------------------------------------
    // Compute Buffers
    // --------------------------------------------------------
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer vertexShapeIndexBuffer;
    private ComputeBuffer shapeInfoBuffer;
    private ComputeBuffer bodyDataBuffer;
    private ComputeBuffer transformedVertexBuffer;

    private int kernelHandle;
    private bool buffersSet = false;

    // --------------------------------------------------------
    // CPU-side Data
    // --------------------------------------------------------
    private List<Vector3> allVertices = new List<Vector3>();
    private List<int> shapeIndexPerVertex = new List<int>();

    // Instead of storing localTransform as Matrix4x4,
    // we store local position + quaternion + body index.
    [StructLayout(LayoutKind.Sequential)]
    private struct ShapeInfo
    {
        public Vector3 shapePos;    // local translation
        public Quaternion shapeRot; // local rotation
        public int bodyIndex;       // which body the shape belongs to
    }
    private List<ShapeInfo> shapeInfos = new List<ShapeInfo>();

    // For each body, we'll store pos + rot in BodyData.
    [StructLayout(LayoutKind.Sequential)]
    private struct BodyData
    {
        public Vector3 pos;
        public Quaternion rot;
    }
    private List<BodyData> bodyDataCPU = new List<BodyData>();

    // --------------------------------------------------------
    // Chrono Body System
    // --------------------------------------------------------
    private vector_ChBody allBodies = new vector_ChBody();

    // --------------------------------------------------------
    // Scene-Change Detection (Track Body Counts)
    // --------------------------------------------------------
    private int prevNumBodies = 0;
    private int prevNumBodiesActive = 0;
    private int prevNumBodiesSleeping = 0;
    private int prevNumBodiesFixed = 0;

    // --------------------------------------------------------
    // Unity Lifecycle
    // --------------------------------------------------------
    void Start()
    {
        // 1) Find our required line-drawing shader
        Shader shader = Shader.Find("Custom/DrawCollisionShape");
        if (shader == null)
        {
            Debug.LogError("'DrawCollisionShape' shader not found.");
            enabled = false;
            return;
        }
        lineMaterial = new Material(shader);

        // 2) Load our transform compute shader
        transformShader = (ComputeShader)Resources.Load("VertexTransform");
        if (transformShader == null)
        {
            Debug.LogError("'VertexTransform' compute shader not found.");
            enabled = false;
            return;
        }

        // 3) Get current Chrono bodies
        allBodies = UChSystem.chrono_system.GetBodies();

        // 4) Build shape data (collect line segments for all shapes)
        BuildShapeData();

        // 5) Create GPU buffers
        InitializeComputeBuffers();

        buffersSet = true;

        // 6) Store initial Chrono body counters for scene-change detection
        prevNumBodies = (int)UChSystem.chrono_system.GetNumBodies();
        prevNumBodiesActive = (int)UChSystem.chrono_system.GetNumBodiesActive();
        prevNumBodiesSleeping = (int)UChSystem.chrono_system.GetNumBodiesSleeping();
        prevNumBodiesFixed = (int)UChSystem.chrono_system.GetNumBodiesFixed();
    }

    // --------------------------------------------------------
    // Build wireframe data (line segments) from each shape
    // in every body.
    // --------------------------------------------------------
    void BuildShapeData()
    {
        allVertices.Clear();
        shapeIndexPerVertex.Clear();
        shapeInfos.Clear();

        int currentShapeIndex = 0;

        for (int b = 0; b < allBodies.Count; b++)
        {
            ChBody body = allBodies[b];
            if (body.GetCollisionModel() == null)
                continue;

            uint numShapes = body.GetCollisionModel().GetNumShapes();
            for (int s = 0; s < numShapes; s++)
            {
                var shapeInstance = body.GetCollisionModel().GetShapeInstance(s);

                // Convert shape frame to position+rotation (instead of matrix)
                Vector3 shapePos;
                Quaternion shapeRot;
                ConvertFrameToPositionRotation(shapeInstance.second, out shapePos, out shapeRot);

                // Prepare a shape info entry
                ShapeInfo sInfo = new ShapeInfo();
                sInfo.shapePos = shapePos;
                sInfo.shapeRot = shapeRot;
                sInfo.bodyIndex = b;
                shapeInfos.Add(sInfo);

                // Gather lines for this shape
                AddShapeLines(shapeInstance.first, currentShapeIndex);

                currentShapeIndex++;
            }
        }
    }

    // --------------------------------------------------------
    // Switch among shape types and build line data
    // --------------------------------------------------------
    private void AddShapeLines(ChCollisionShape shape, int currentShapeIndex)
    {
        ChCollisionShape.Type shapeType = shape.GetType();
        switch (shapeType)
        {
            case ChCollisionShape.Type.BOX:
                AddBoxLines(shape, currentShapeIndex);
                break;

            case ChCollisionShape.Type.SPHERE:
                AddSphereLines(shape, currentShapeIndex);
                break;

            case ChCollisionShape.Type.CYLINDER:
                AddCylinderLines(shape, currentShapeIndex);
                break;

            case ChCollisionShape.Type.TRIANGLEMESH:
                AddTriangleMeshLines(shape, currentShapeIndex);
                break;

            case ChCollisionShape.Type.CONVEXHULL:
                AddConvexHullLines(shape, currentShapeIndex);
                break;

            default:
                Debug.LogWarning("No code to render a " + shapeType);
                break;
        }
    }

    // --------------------------------------------------------
    // Initialize GPU buffers from CPU data
    // --------------------------------------------------------
    private void InitializeComputeBuffers()
    {
        // Release existing buffers (if re-initializing)
        ReleaseBuffers();

        // 1) Vertex positions
        vertexBuffer = new ComputeBuffer(allVertices.Count, sizeof(float) * 3);
        vertexBuffer.SetData(allVertices);

        // 2) Which shape each vertex belongs to
        vertexShapeIndexBuffer = new ComputeBuffer(shapeIndexPerVertex.Count, sizeof(int));
        vertexShapeIndexBuffer.SetData(shapeIndexPerVertex);

        // 3) The shape info array
        // Each ShapeInfo: Vector3 (12 bytes) + Quaternion (16 bytes) + int (4 bytes) = 32 bytes total
        shapeInfoBuffer = new ComputeBuffer(shapeInfos.Count, 32);
        shapeInfoBuffer.SetData(shapeInfos);

        // 4) The body data array: Vector3 pos (12 bytes) + Quaternion rot (16 bytes) = 28 bytes
        // For simplicity, we use 32 bytes stride or you can keep 28. Example:
        int bodyStride = 12 + 16; // = 28
        bodyDataBuffer = new ComputeBuffer(allBodies.Count, bodyStride);

        // 5) The final transformed positions
        transformedVertexBuffer = new ComputeBuffer(allVertices.Count, sizeof(float) * 3);

        // Bind to the compute shader
        kernelHandle = transformShader.FindKernel("CSMain");
        transformShader.SetBuffer(kernelHandle, "vertices", vertexBuffer);
        transformShader.SetBuffer(kernelHandle, "vertexShapeIndices", vertexShapeIndexBuffer);
        transformShader.SetBuffer(kernelHandle, "shapeInfos", shapeInfoBuffer);
        transformShader.SetBuffer(kernelHandle, "bodyData", bodyDataBuffer);
        transformShader.SetBuffer(kernelHandle, "transformedVertices", transformedVertexBuffer);

        // Pass array lengths to the compute shader
        transformShader.SetInt("vertexCount", allVertices.Count);
        transformShader.SetInt("shapeCount", shapeInfos.Count);
        transformShader.SetInt("bodyCount", allBodies.Count);
    }

    // --------------------------------------------------------
    // Called every frame
    // --------------------------------------------------------
    void Update()
    {
        if (!buffersSet)
            return;

        // 1) Detect changes in the Chrono system (body counts)
        int numBodies = (int)UChSystem.chrono_system.GetNumBodies();
        int numBodiesActive = (int)UChSystem.chrono_system.GetNumBodiesActive();
        int numBodiesSleeping = (int)UChSystem.chrono_system.GetNumBodiesSleeping();
        int numBodiesFixed = (int)UChSystem.chrono_system.GetNumBodiesFixed();

        // If there's any difference from last frame, we do a "scene rebuild"
        if (numBodies != prevNumBodies ||
            numBodiesActive != prevNumBodiesActive ||
            numBodiesSleeping != prevNumBodiesSleeping ||
            numBodiesFixed != prevNumBodiesFixed)
        {
            // Rebuild shape data and buffers
            RebuildSceneData();

            // Update stored counters
            prevNumBodies = numBodies;
            prevNumBodiesActive = numBodiesActive;
            prevNumBodiesSleeping = numBodiesSleeping;
            prevNumBodiesFixed = numBodiesFixed;
        }

        // 2) Update body transforms (pos + rot)
        UpdateBodyData();

        // 3) Dispatch the compute shader
        int groups = Mathf.CeilToInt(allVertices.Count / 256.0f);
        transformShader.Dispatch(kernelHandle, groups, 1, 1);
    }

    /// <summary>
    /// Rebuilds the entire set of shape data and reinitializes buffers.
    /// Called if we detect that the Chrono system changed.
    /// </summary>
    private void RebuildSceneData()
    {
        // Refresh the pointer to bodies (in case new bodies were added)
        allBodies = UChSystem.chrono_system.GetBodies();

        // Rebuild shape data from scratch
        BuildShapeData();

        // Recreate/resize buffers
        InitializeComputeBuffers();
    }

    /// <summary>
    /// Updates the bodyData buffer each frame.
    /// </summary>
    private void UpdateBodyData()
    {
        bodyDataCPU.Clear();
        for (int i = 0; i < allBodies.Count; i++)
        {
            Vector3 bPos;
            Quaternion bRot;
            ConvertFrameToPositionRotation(allBodies[i].GetFrameRefToAbs(), out bPos, out bRot);

            BodyData bd = new BodyData();
            bd.pos = bPos;
            bd.rot = bRot;  // Optionally .normalized
            bodyDataCPU.Add(bd);
        }
        bodyDataBuffer.SetData(bodyDataCPU);
    }

    // --------------------------------------------------------
    // Render
    // --------------------------------------------------------
    void OnRenderObject()
    {
        if (!buffersSet)
            return;

        lineMaterial.SetPass(0);
        lineMaterial.SetBuffer("vertexPositions", transformedVertexBuffer);
        Graphics.DrawProceduralNow(MeshTopology.Lines, transformedVertexBuffer.count, 1);
    }

    // --------------------------------------------------------
    // Cleanup
    // --------------------------------------------------------
    void OnDestroy()
    {
        ReleaseBuffers();
    }

    private void ReleaseBuffers()
    {
        vertexBuffer?.Release();
        vertexShapeIndexBuffer?.Release();
        shapeInfoBuffer?.Release();
        transformedVertexBuffer?.Release();
        bodyDataBuffer?.Release();
    }

    // ---------------------------------------------------------
    // HELPER: Convert ChFramed => position + quaternion
    // ---------------------------------------------------------
    private void ConvertFrameToPositionRotation(ChFramed frame, out Vector3 pos, out Quaternion rot)
    {
        frame.Normalize();
        pos = Utils.FromChronoFlip(frame.GetPos());
        rot = Utils.FromChronoFlip(frame.GetRot());
    }

    // ---------------------------------------------------------
    //  Shape-building helper methods
    // ---------------------------------------------------------

    private void AddBoxLines(ChCollisionShape colShape, int shapeIndex)
    {
        var chronoBox = chrono.CastToChCollisionShapeBox(colShape);
        Vector3 halfExtents = Utils.FromChronoFlip(chronoBox.GetHalflengths());

        Vector3 center = Vector3.zero;
        Vector3[] corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            corners[i] = center + new Vector3(
                (i & 1) == 0 ? -halfExtents.x : halfExtents.x,
                (i & 2) == 0 ? -halfExtents.y : halfExtents.y,
                (i & 4) == 0 ? -halfExtents.z : halfExtents.z
            );
        }

        int[][] edges = {
            new int[] { 0, 1 }, new int[] { 1, 3 }, new int[] { 3, 2 }, new int[] { 2, 0 },
            new int[] { 4, 5 }, new int[] { 5, 7 }, new int[] { 7, 6 }, new int[] { 6, 4 },
            new int[] { 0, 4 }, new int[] { 1, 5 }, new int[] { 2, 6 }, new int[] { 3, 7 }
        };

        foreach (int[] edge in edges)
        {
            AddLine(corners[edge[0]], corners[edge[1]], shapeIndex);
        }
    }

    private void AddSphereLines(ChCollisionShape colShape, int shapeIndex)
    {
        var chronoSphere = chrono.CastToChCollisionShapeSphere(colShape);
        float radius = (float)chronoSphere.GetRadius();

        int latSegments = 12;
        int lonSegments = 24;

        for (int lat = 0; lat <= latSegments; lat++)
        {
            float phi1 = Mathf.PI * lat / latSegments;
            float phi2 = Mathf.PI * (lat + 1) / latSegments;

            for (int lon = 0; lon <= lonSegments; lon++)
            {
                float theta1 = 2f * Mathf.PI * lon / lonSegments;
                float theta2 = 2f * Mathf.PI * (lon + 1) / lonSegments;

                Vector3 p1 = SphereCoord(radius, phi1, theta1);
                Vector3 p2 = SphereCoord(radius, phi1, theta2);
                AddLine(p1, p2, shapeIndex);

                // Vertical lines
                if (lat < latSegments)
                {
                    Vector3 p3 = SphereCoord(radius, phi2, theta1);
                    AddLine(p1, p3, shapeIndex);
                }
            }
        }
    }

    private void AddCylinderLines(ChCollisionShape colShape, int shapeIndex)
    {
        var chronoCyl = chrono.CastToChCollisionShapeCylinder(colShape);
        float radius = (float)chronoCyl.GetRadius();
        float halfLength = (float)chronoCyl.GetHeight() / 2;

        int segments = 24;
        float step = 2f * Mathf.PI / segments;

        for (int i = 0; i < segments; i++)
        {
            float theta1 = i * step;
            float theta2 = (i + 1) * step;

            // Top circle (z = +halfLength)
            Vector3 t1 = new Vector3(radius * Mathf.Cos(theta1), radius * Mathf.Sin(theta1), halfLength);
            Vector3 t2 = new Vector3(radius * Mathf.Cos(theta2), radius * Mathf.Sin(theta2), halfLength);
            AddLine(t1, t2, shapeIndex);

            // Bottom circle (z = -halfLength)
            Vector3 b1 = new Vector3(radius * Mathf.Cos(theta1), radius * Mathf.Sin(theta1), -halfLength);
            Vector3 b2 = new Vector3(radius * Mathf.Cos(theta2), radius * Mathf.Sin(theta2), -halfLength);
            AddLine(b1, b2, shapeIndex);

            // Vertical line connecting top/bottom
            AddLine(t1, b1, shapeIndex);
        }
    }

    private void AddTriangleMeshLines(ChCollisionShape colShape, int shapeIndex)
    {
        var chronoMesh = chrono.CastToChCollisionShapeTriangleMesh(colShape);
        var mesh = chronoMesh.GetMesh();
        uint nt = mesh.GetNumTriangles();
        for (uint i = 0; i < nt; i++)
        {
            var tri = mesh.GetTriangle(i);
            Vector3 p1 = Utils.FromChronoFlip(tri.p1);
            Vector3 p2 = Utils.FromChronoFlip(tri.p2);
            Vector3 p3 = Utils.FromChronoFlip(tri.p3);

            AddLine(p1, p2, shapeIndex);
            AddLine(p2, p3, shapeIndex);
            AddLine(p3, p1, shapeIndex);
        }
    }

    private void AddConvexHullLines(ChCollisionShape colShape, int shapeIndex)
    {
        var chronoHull = chrono.CastToChCollisionShapeConvexHull(colShape);
        vector_ChVector3d hullPoints = chronoHull.GetPoints();
        // For demonstration, we add each point as lines, but
        // you might want actual edges if available.
        foreach (var point in hullPoints)
        {
            Vector3 v = Utils.FromChronoFlip(point);
            allVertices.Add(v);
            shapeIndexPerVertex.Add(shapeIndex);
        }
    }

    // --------------------------------------------------------
    // Utilities
    // --------------------------------------------------------
    private void AddLine(Vector3 start, Vector3 end, int shapeIndex)
    {
        allVertices.Add(start);
        shapeIndexPerVertex.Add(shapeIndex);

        allVertices.Add(end);
        shapeIndexPerVertex.Add(shapeIndex);
    }

    /// <summary>
    /// Spherical coordinate helper for wireframe spheres
    /// </summary>
    private Vector3 SphereCoord(float r, float phi, float theta)
    {
        float sinPhi = Mathf.Sin(phi);
        float x = r * sinPhi * Mathf.Cos(theta);
        float y = r * sinPhi * Mathf.Sin(theta);
        float z = r * Mathf.Cos(phi);
        return new Vector3(x, y, z);
    }
}
