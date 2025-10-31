using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(500)]
public class CollisionMeshAllBodies : MonoBehaviour
{
    // --------------------------------------------------------
    // Shaders and Settings
    // --------------------------------------------------------
    [Header("Shaders")]
    [Tooltip("Auto-loaded from Resources if not assigned")]
    public ComputeShader transformShader;
    
    [HideInInspector]
    public Shader drawShader;
    
    [Header("Line Settings")]
    public Color lineColor = Color.cyan;
    public float maxDistance = 25f;
    
    [Header("Optimization")]
    [Tooltip("Only rebuild geometry when collision shapes change")]
    public bool useLazyRefresh = true;
    
    private Material lineMaterial;

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
    private List<Vector3> allVertices = new List<Vector3>(8192);
    private List<int> shapeIndexPerVertex = new List<int>(8192);

    // Instead of storing localTransform as Matrix4x4,
    // we store local position + quaternion + body index
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct ShapeInfo
    {
        public Vector3 shapePos;    // local translation
        public float _pad0;
        public Quaternion shapeRot; // local rotation
        public int bodyIndex;       // which body the shape belongs to
        public int _pad1, _pad2, _pad3;
    }
    private List<ShapeInfo> shapeInfos = new List<ShapeInfo>(512);

    // For each body, store pos + rot in BodyData - this can be somewhat complicated if too many but should
    // still be faster than non-cached approach (more mem tho)
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct BodyData
    {
        public Vector3 pos;
        public float _pad0;
        public Quaternion rot;
    }
    private List<BodyData> bodyDataCPU = new List<BodyData>(256);

    // --------------------------------------------------------
    // Chrono Body System
    // --------------------------------------------------------
    private vector_ChBody allBodies = new vector_ChBody();

    // --------------------------------------------------------
    // Scene-Change Detection - track body count
    // TODO - for dynamic mesh (in future!) - this will need more work
    // --------------------------------------------------------
    private int prevNumBodies = 0;
    private int prevNumBodiesActive = 0;
    private int prevNumBodiesSleeping = 0;
    private int prevNumBodiesFixed = 0;
    private int framesSinceRebuild = 0;
    private const int REBUILD_CHECK_INTERVAL = 30;
    private bool geometryDirty = true;

    void Start()
    {
        // Find the line-drawing shader
        if (drawShader == null)
        {
            drawShader = Shader.Find("Custom/DrawCollisionShape");
            if (drawShader == null)
            {
                Debug.LogError("DrawCollisionShape shader not found via Inspector or Shader.Find");
                enabled = false;
                return;
            }
        }
        lineMaterial = new Material(drawShader);
        lineMaterial.SetColor("_Color", lineColor);
        lineMaterial.SetFloat("_MaxDistance", maxDistance);

        // Load our transform compute shader
        if (transformShader == null)
        {
            transformShader = Resources.Load<ComputeShader>("VertexTransform");
            if (transformShader == null)
            {
                Debug.LogError("VertexTransform compute shader missing or not in Resources/");
                enabled = false;
                return;
            }
        }
        
        kernelHandle = transformShader.FindKernel("CSMain");
        if (kernelHandle < 0)
        {
            Debug.LogError("CSMain kernel not found in transformShader");
            enabled = false;
            return;
        }

        // Get current Chrono bodies
        allBodies = UChSystem.chrono_system.GetBodies();

        // Build shape data (collect line segments for all shapes)
        BuildShapeData();

        // Create GPU buffers
        InitializeComputeBuffers();

        // Store initial Chrono body counters for scene-change detection
        prevNumBodies = (int)UChSystem.chrono_system.GetNumBodies();
        prevNumBodiesActive = (int)UChSystem.chrono_system.GetNumBodiesActive();
        prevNumBodiesSleeping = (int)UChSystem.chrono_system.GetNumBodiesSleeping();
        prevNumBodiesFixed = (int)UChSystem.chrono_system.GetNumBodiesFixed();
        
        geometryDirty = false;
    }
    
    void OnEnable()
    {
        RenderPipelineManager.endCameraRendering += OnRenderCallback;
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnRenderCallback;
    }

    // --------------------------------------------------------
    // Build wireframe data (line segments) from each shape
    // in every body
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
                ConvertFrameToPositionRotation(shapeInstance.frame, out shapePos, out shapeRot);

                // Prepare a shape info entry
                ShapeInfo sInfo = new ShapeInfo
                {
                    shapePos = shapePos,
                    shapeRot = shapeRot,
                    bodyIndex = b,
                    _pad0 = 0,
                    _pad1 = 0,
                    _pad2 = 0,
                    _pad3 = 0
                };
                shapeInfos.Add(sInfo);

                // Gather lines for this shape
                AddShapeLines(shapeInstance.shape, currentShapeIndex);

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

        if (allVertices.Count == 0)
        {
            buffersSet = false;
            return;
        }

        // Vertex positions
        vertexBuffer = new ComputeBuffer(allVertices.Count, 12, ComputeBufferType.Default);
        vertexBuffer.SetData(allVertices);

        // Which shape each vertex belongs to
        vertexShapeIndexBuffer = new ComputeBuffer(shapeIndexPerVertex.Count, 4, ComputeBufferType.Default);
        vertexShapeIndexBuffer.SetData(shapeIndexPerVertex);

        // The shape info array (48 bytes with padding)
        shapeInfoBuffer = new ComputeBuffer(shapeInfos.Count, 48, ComputeBufferType.Default);
        shapeInfoBuffer.SetData(shapeInfos);

        // The body data array (32 bytes with padding)
        bodyDataBuffer = new ComputeBuffer(allBodies.Count, 32, ComputeBufferType.Default);

        // The final transformed positions
        transformedVertexBuffer = new ComputeBuffer(allVertices.Count, 12, ComputeBufferType.Default);

        // Bind to the compute shader
        kernelHandle = transformShader.FindKernel("CSMain");
        transformShader.SetBuffer(kernelHandle, "vertices", vertexBuffer);
        transformShader.SetBuffer(kernelHandle, "vertexShapeIndices", vertexShapeIndexBuffer);
        transformShader.SetBuffer(kernelHandle, "shapeInfos", shapeInfoBuffer);
        transformShader.SetBuffer(kernelHandle, "bodyData", bodyDataBuffer);
        transformShader.SetBuffer(kernelHandle, "transformedVertices", transformedVertexBuffer);

        // Pass array lengths to the compute shader (so GPU knows sizes)
        transformShader.SetInt("vertexCount", allVertices.Count);
        transformShader.SetInt("shapeCount", shapeInfos.Count);
        transformShader.SetInt("bodyCount", allBodies.Count);
        
        buffersSet = true;
    }

    void Update()
    {
        if (!buffersSet)
            return;

        // Check for scene changes based on lazy refresh setting
        if (useLazyRefresh)
        {
            framesSinceRebuild++;

            // Only check for scene changes periodically (not every frame)
            if (framesSinceRebuild >= REBUILD_CHECK_INTERVAL)
            {
                framesSinceRebuild = 0;
                
                // Detect changes in the Chrono system (body counts)
                int numBodies = (int)UChSystem.chrono_system.GetNumBodies();
                int numBodiesActive = (int)UChSystem.chrono_system.GetNumBodiesActive();
                int numBodiesSleeping = (int)UChSystem.chrono_system.GetNumBodiesSleeping();
                int numBodiesFixed = (int)UChSystem.chrono_system.GetNumBodiesFixed();

                // If change, rebuild geometry
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
                    geometryDirty = false;
                }
            }
        }
        else
        {
            // No lazy refresh - always rebuild every frame 
            RebuildSceneData();
        }

        // Update body transforms (pos + rot)
        UpdateBodyData();

        // Dispatch the compute shader - use ceiling division without Mathf
        int groups = (allVertices.Count + 255) / 256;
        transformShader.Dispatch(kernelHandle, groups, 1, 1);
    }

    /// Rebuilds the entire set of shape data and reinitializes buffers.
    /// Called if we detect that the Chrono system changed.
    private void RebuildSceneData()
    {
        // Refresh the pointer to bodies (in case new bodies were added)
        allBodies = UChSystem.chrono_system.GetBodies();

        // Rebuild shape data from scratch
        BuildShapeData();

        // Recreate/resize buffers
        InitializeComputeBuffers();
    }

    /// Updates the bodyData buffer each frame
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
            bd.rot = bRot;
            bodyDataCPU.Add(bd);
        }
        bodyDataBuffer.SetData(bodyDataCPU);
    }

    /// Forces a rebuild of collision geometry
   public void ForceGeometryRebuild()
    {
        if (!buffersSet) return;
        
        allBodies = UChSystem.chrono_system.GetBodies();
        BuildShapeData();
        InitializeComputeBuffers();
        
        prevNumBodies = (int)UChSystem.chrono_system.GetNumBodies();
        prevNumBodiesActive = (int)UChSystem.chrono_system.GetNumBodiesActive();
        prevNumBodiesSleeping = (int)UChSystem.chrono_system.GetNumBodiesSleeping();
        prevNumBodiesFixed = (int)UChSystem.chrono_system.GetNumBodiesFixed();
        geometryDirty = false;
    }

    // --------------------------------------------------------
    // URP Multi-Camera Rendering Update
    // --------------------------------------------------------
    void OnRenderCallback(ScriptableRenderContext context, Camera camera)
    {
        // Bail out if not ready or disabled
        if (!buffersSet || !isActiveAndEnabled)
            return;

        // Skip if no vertices to draw
        if (transformedVertexBuffer == null || transformedVertexBuffer.count == 0)
            return;

        // Update material properties per-camera
        lineMaterial.SetColor("_Color", lineColor);
        lineMaterial.SetFloat("_MaxDistance", maxDistance);
        lineMaterial.SetBuffer("vertexPositions", transformedVertexBuffer);

        // Draw procedural lines using command buffer for URP
        CommandBuffer cmd = CommandBufferPool.Get("CollisionMeshLines");
        cmd.DrawProcedural(Matrix4x4.identity, lineMaterial, 0, MeshTopology.Lines, transformedVertexBuffer.count);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    // --------------------------------------------------------
    // Used for Scene View
    // --------------------------------------------------------
    void OnRenderObject()
    {
        if (!buffersSet || !isActiveAndEnabled)
            return;

        // Update material properties
        lineMaterial.SetColor("_Color", lineColor);
        lineMaterial.SetFloat("_MaxDistance", maxDistance);
        lineMaterial.SetBuffer("vertexPositions", transformedVertexBuffer);
        
        lineMaterial.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Lines, transformedVertexBuffer.count);
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
    // HELPER: Convert ChFramed to position + quaternion
    // ---------------------------------------------------------
    private void ConvertFrameToPositionRotation(ChFramed frame, out Vector3 pos, out Quaternion rot)
    {
        frame.Normalize();
        pos = Utils.FromChronoFlip(frame.GetPos());
        rot = Utils.FromChronoFlip(frame.GetRot());
    }

    // ---------------------------------------------------------
    //  Shape-building helpers - this is a bit crude and doesn't
    // handle all cases
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
        // Add each point as lines, but only gives the general idea of the shape,
        // not the makeup of the mesh
        foreach (var point in hullPoints)
        {
            Vector3 v = Utils.FromChronoFlip(point);
            allVertices.Add(v);
            shapeIndexPerVertex.Add(shapeIndex);
        }
    }

    // Just a quick helper for line segments
    private void AddLine(Vector3 start, Vector3 end, int shapeIndex)
    {
        allVertices.Add(start);
        shapeIndexPerVertex.Add(shapeIndex);

        allVertices.Add(end);
        shapeIndexPerVertex.Add(shapeIndex);
    }

    /// Spherical coordinate helper for wireframe spheres
    private Vector3 SphereCoord(float r, float phi, float theta)
    {
        float sinPhi = Mathf.Sin(phi);
        float x = r * sinPhi * Mathf.Cos(theta);
        float y = r * sinPhi * Mathf.Sin(theta);
        float z = r * Mathf.Cos(phi);
        return new Vector3(x, y, z);
    }
}
