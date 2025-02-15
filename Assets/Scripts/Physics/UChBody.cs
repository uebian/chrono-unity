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

using UnityEngine;
// Ensure this base script executes just after the Chsystem, but before everything else.
[DefaultExecutionOrder(-999)]
public class UChBody : MonoBehaviour
{
    public bool isFixed;
    public bool collide;
    public bool showFrameGizmo;
    public float comRadiusGizmo;

    public bool automaticMass;
    public float density;
    public Vector3 COM;
    public double mass;
    public Vector3 inertiaMoments;
    public Vector3 inertiaProducts;

    public Vector3 linearVelocity;
    public Vector3 angularVelocity;

    protected ChBodyAuxRef body;

    public UChBody()
    {
        isFixed = false;
        collide = true;
        showFrameGizmo = false;
        comRadiusGizmo = 0.1f;
        automaticMass = false;
        density = 1000;
        mass = 100; // this is used as a backup only, to avoid crashes with zero-mass objects.
        inertiaMoments = Vector3.one;
        inertiaProducts = Vector3.zero;
    }

    public ChBodyAuxRef GetChBody()
    {
        return body;
    }

    public virtual void Create()
    {
        body = new ChBodyAuxRef();
    }

    public void Destroy()
    {
        // cleanly remove the body from the system
        // note: this intentionally will not destroy the Unity gameobject. That must be handled elsewhere
        UChSystem.chrono_system.RemoveBody(body);
        body = null;
    }

    public virtual void AddToSystem()
    {
        // Avoid crash if adding object that's not been instantised
        if (body == null)
        {
            Debug.Log("No body to add to system (" + gameObject.name + ")");
        }
        else
        {
            UChSystem.chrono_system.AddBody(body);
        }
        DebugInfo();
    }

    public virtual void CalculateMassProperties() { }

    public virtual void OnDrawGizmosExtra() { }

    public void DebugInfo()
    {
        Debug.Log("Name:      " + gameObject.name + "\n" +
                  "Mass:      " + body.GetMass() + "\n" +
                  "COM:       " + Utils.FromChronoFlip(body.GetFrameCOMToRef().GetPos()) + "\n" + // flip the COM out of chrono
                  "inertiaXX: " + Utils.FromChrono(body.GetInertiaXX()) + "\n" +
                  "inertiaXY: " + Utils.FromChronoFlip(body.GetInertiaXY())); // convert chrono RHF interia to the LHF unity for user to read in unity context
    }

    public virtual void InstanceCreation()
    {
        ///Debug.Log("Body Awake()");
        if (body == null)
            Create(); // if this is called by a generator script, ensure we create a body.
        
        // only calculate mass properties if the auto mass is ticked, otherwise, skip and assume user-entered values.
        if (automaticMass)
            CalculateMassProperties();
        
        // intertia and mass now should be either set by user, or auto calculated with the override
        body.SetMass(mass);
        // Need flipping to ensure correct placement
        body.SetFrameCOMToRef(new ChFramed(Utils.ToChronoFlip(COM)));
        body.SetInertiaXX(Utils.ToChrono(inertiaMoments)); // no flipping on the diagonal - because a pure z→−zz→−z reflection does not change Ixx, Iyy, Izz
        body.SetInertiaXY(Utils.ToChronoFlip(inertiaProducts)); // flip the Z inertia.

        body.SetFixed(isFixed);
        body.EnableCollision(collide);

        body.SetFrameRefToAbs(new ChFramed(Utils.ToChronoFlip(transform.position), Utils.ToChronoFlip(transform.rotation)));

        body.SetPosDt(Utils.ToChronoFlip(linearVelocity));
        body.SetAngVelLocal(Utils.ToChronoFlip(angularVelocity));

        ////DebugInfo();
        if (UChSystem.chrono_system != null)
        { AddToSystem(); }
    }

    // ---  UNITY METHODS ---
    void Awake()
    {
        InstanceCreation();
    }

    public void Start()
    {
        // Do nothing.
    }

    void Update()
    {
        ////Debug.Log("body Time = " + body.GetChTime());

        // Update body state
        var frame = body.GetFrameRefToAbs();
        // Ensure flipped for chrono-unity
        transform.position = Utils.FromChronoFlip(frame.GetPos());
        transform.rotation = Utils.FromChronoFlip(frame.GetRot());
        linearVelocity = Utils.FromChronoFlip(frame.GetPosDt());
        angularVelocity = Utils.FromChronoFlip(frame.GetAngVelLocal());
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(1, 1, 1));

        if (showFrameGizmo)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(Vector3.zero, new Vector3(2, 0, 0));
            Gizmos.color = Color.green;
            Gizmos.DrawLine(Vector3.zero, new Vector3(0, 2, 0));
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(Vector3.zero, new Vector3(0, 0, 2));

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(COM, comRadiusGizmo);
        }

        OnDrawGizmosExtra();
    }
}
