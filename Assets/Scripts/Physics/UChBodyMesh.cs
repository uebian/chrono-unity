// =============================================================================
// PROJECT CHRONO - http://projectchrono.org
//
// Copyright (c) 2024 projectchrono.org
// All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found
// in the LICENSE file at the top level of the distribution.
//
// =============================================================================
// Authors: Radu Serban, Josh Diyn
// =============================================================================

using System;
using UnityEngine;

public class UChBodyMesh : UChBody
{
    private ChContactMaterial mat;

    public bool collisionMesh;
    public string collisionMeshOBJFile;

    public double sweptSphereRadius;

    public UChBodyMesh()
    {
        automaticMass = true;
        collisionMesh = false;
        sweptSphereRadius = 0;
    }

    public override void Create()
    {
        // Get a handle to the associated material component and create the Chrono material
        var mat_component = this.GetComponent<UChMaterialSurface>();
        ////mat_component.DebugInfo();
        mat = mat_component.mat_info.CreateMaterial(mat_component.contact_method);

        // Create Chrono collision mesh
        var chrono_mesh = new ChTriangleMeshConnected();
        // get local scale of this object
        var scl = transform.localScale;


        bool use_file = false;
        if (collisionMesh)
        {
            var ext = System.IO.Path.GetExtension(collisionMeshOBJFile);
            ////Debug.Log(gameObject.name + "  -- Collision mesh file extension: " + ext);
            use_file = System.IO.File.Exists(collisionMeshOBJFile) && String.Equals(ext, ".obj");
            if (!use_file)
            {
                Debug.Log(gameObject.name + "  -- Specified OBJ file " + collisionMeshOBJFile + " does not exist or is incorrect type.\nUsing mesh filter!");
            }
        }
        if (use_file)
        {
            // Load the OBJ mesh (no normals, no UV)
            chrono_mesh.LoadWavefrontMesh(collisionMeshOBJFile, false, false);

            // Scale all vertices to the local.transform (scl)
            for (int i = 0; i < chrono_mesh.m_vertices.Count; i++)
            {
                ChVector3d v = chrono_mesh.m_vertices[i];
                chrono_mesh.m_vertices[i] = new ChVector3d(
                    scl.x * v.x,
                    scl.y * v.y,
                    scl.z * v.z
                );
            }
            // Flip 180 about Y then recompute normals, thiis is faster than the meshfilter
            // metod below since it avoids having to swap the winding and we're redoing the normals and face vertices
            chrono_mesh.Transform(new ChVector3d(), chrono.QuatFromAngleY(chrono.CH_PI));

            // Compute vertex normals by accumulating over faces,then normalising (smoother)
            vector_ChVector3d accumulatedNormals = new vector_ChVector3d(chrono_mesh.m_vertices.Count);
            for (int i = 0; i < chrono_mesh.m_vertices.Count; i++)
                accumulatedNormals.Add(new ChVector3d(0, 0, 0)); // set to naught

            for (int f = 0; f < chrono_mesh.m_face_v_indices.Count; f++)
            {
                // get the face indices
                ChVector3i face = chrono_mesh.m_face_v_indices[f];
                var i0 = face.x;
                var i1 = face.y;
                var i2 = face.z;

                ChVector3d p0 = chrono_mesh.m_vertices[i0];
                ChVector3d p1 = chrono_mesh.m_vertices[i1];
                ChVector3d p2 = chrono_mesh.m_vertices[i2];

                ChVector3d e1 = new ChVector3d();
                ChVector3d e2 = new ChVector3d();
                e1.Sub(p1, p0); // Set this vector to the difference of A and B: this = A - B
                e2.Sub(p2, p0);
                ChVector3d faceNormal = chrono.Vcross(e1, e2); // new face normal

                // Accumulate into each of the face's vertices
                accumulatedNormals[i0].Add(accumulatedNormals[i0], faceNormal); // Set this vector to the sum of A and B: this = A + B
                accumulatedNormals[i1].Add(accumulatedNormals[i1], faceNormal);
                accumulatedNormals[i2].Add(accumulatedNormals[i2], faceNormal);
            }

            chrono_mesh.m_normals.Capacity = accumulatedNormals.Count;
            for (int i = 0; i < accumulatedNormals.Count; i++)
            {
                ChVector3d nrm = accumulatedNormals[i];
                double length = nrm.Length();
                if (length < 1e-14)
                    nrm = new ChVector3d(0, 1, 0); // degenerate case. arbitrary fallback
                else
                    nrm.Scale(1.0 / length);
                // send the new normals tochrono
                chrono_mesh.m_normals.Add(nrm);
            }
            // set the face vertex indicies
            for (int f = 0; f < chrono_mesh.m_face_v_indices.Count; f++)
            {
                chrono_mesh.m_face_n_indices.Add(chrono_mesh.m_face_v_indices[f]);
            }
        }
        else {
            // Using the meshfilter for collision geometry
            ////Debug.Log(gameObject.name + "  -- Using mesh filter for collision");
            var mesh_component = this.GetComponent<MeshFilter>();
            var mesh = mesh_component.mesh;

            // Reserve space in the Chrono mesh
            chrono_mesh.m_vertices.Capacity = mesh.vertices.Length;
            chrono_mesh.m_face_v_indices.Capacity = mesh.triangles.Length / 3;

            // Copy vertices to Chrono, applying local scaling & flip
            for (int i = 0; i < mesh.vertices.Length; i++)
            {
                Vector3 v = mesh.vertices[i];
                // Apply local scaling & mirror the Z (Unity LHF to Chrono RHF)
                double px = scl.x * (double)v.x;
                double py = scl.y * (double)v.y;
                double pz = scl.z * (double)-v.z;
                // add the vertices into the chrono mesh
                chrono_mesh.m_vertices.Add(new ChVector3d(px, py, pz));
            }
            // Set up the face vertex indices. NB the 2nd and 3rd indice are swapped!!
            // This is to fix winding and ensure Chrono sees the correct orientation
            for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                var idx0 = mesh.triangles[i];
                var idx1 = mesh.triangles[i + 2];  // swapped
                var idx2 = mesh.triangles[i + 1];  // swapped

                var faceIndices = new ChVector3i(idx0, idx1, idx2);
                chrono_mesh.m_face_v_indices.Add(faceIndices);
            }

            // Since the indicies are swapped we need to recalc vertex normals so Chrono doesn't think
            // that the outside is the inside of the mesh body

            // temp array to accumulate normals per vertex
            vector_ChVector3d accumulatedNormals = new vector_ChVector3d(chrono_mesh.m_vertices.Count);
            for (int i = 0; i < chrono_mesh.m_vertices.Count; i++)
                accumulatedNormals.Add(new ChVector3d(0, 0, 0));

            // Loop over each face, compute the face normal, add it to each vertex of that face
            for (int f = 0; f < chrono_mesh.m_face_v_indices.Count; f++)
            {
                ChVector3i face = chrono_mesh.m_face_v_indices[f];
                int i0 = face.x;
                int i1 = face.y;
                int i2 = face.z;

                // Vertex positions
                ChVector3d p0 = chrono_mesh.m_vertices[i0];
                ChVector3d p1 = chrono_mesh.m_vertices[i1];
                ChVector3d p2 = chrono_mesh.m_vertices[i2];

                // Face normal = cross(p1 - p0, p2 - p0)
                // roundabout way to do this
                ChVector3d e1 = new ChVector3d();
                ChVector3d e2 = new ChVector3d();
                e1.Sub(p1, p0); // Set this vector to the difference of A and B: this = A - B
                e2.Sub(p2, p0);
                ChVector3d faceNormal = chrono.Vcross(e1, e2); // new face normal

                // Accumulate into each of the face's vertices
                accumulatedNormals[i0].Add(accumulatedNormals[i0], faceNormal); // Set this vector to the sum of A and B: this = A + B
                accumulatedNormals[i1].Add(accumulatedNormals[i1], faceNormal);
                accumulatedNormals[i2].Add(accumulatedNormals[i2], faceNormal);
            }

            // Normalise each accumulated normal to get the “smoothed” direction for
            // applying to the vertices in chrono's m_normals... probably a bit of overkill if large mesh, might slow down
            chrono_mesh.m_normals.Capacity = accumulatedNormals.Count;
            for (int i = 0; i < accumulatedNormals.Count; i++)
            {
                ChVector3d nrm = accumulatedNormals[i];
                double length = nrm.Length();
                if (length < 1e-14)
                {
                    // Degenerate case, fallback to an up vector or zero
                    nrm = new ChVector3d(0, 1, 0);
                }
                else
                {
                    nrm.Scale(1 / length);
                }
                chrono_mesh.m_normals.Add(nrm);
            }

            // face_n_indices can use the same indices as face_v_indices because each
            // vertex has its own normal in the same order
            for (int f = 0; f < chrono_mesh.m_face_v_indices.Count; f++)
            {
                var face = chrono_mesh.m_face_v_indices[f];
                chrono_mesh.m_face_n_indices.Add(face); // same triple, referencing the normal array
            }
        }

        // NB - orientation of frame and position is RHF-LHF flipped in parent class (UChBody)after create()
        // Create the body for chrono
        body = new ChBodyEasyMesh(chrono_mesh, density, automaticMass, false, true, mat, 0.01);
        
        // Update UChBody properties
        if (automaticMass)
        {
            mass = body.GetMass();
            COM = Utils.FromChronoFlip(body.GetFrameCOMToRef().GetPos());
            inertiaMoments = Utils.FromChrono(body.GetInertiaXX());
            inertiaProducts = Utils.FromChrono(body.GetInertiaXY());
        }
    }

    public override void AddToSystem()
    {
        base.AddToSystem();

        if (mat.GetContactMethod() != UChSystem.chrono_system.GetContactMethod())
            throw new Exception("Incompatible contact method (" + gameObject.name + ")");
    }
}
