// =============================================================================
// PROJECT CHRONO - http://projectchrono.org
//
// Copyright (c) 2025 projectchrono.org
// All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found
// in the LICENSE file at the top level of the distribution.
//
// =============================================================================
// Authors: Bocheng Zou
// =============================================================================

using System.Diagnostics;
using System.Transactions;
using UnityEngine;

// The same as UChVehicle (which is at -900)
[DefaultExecutionOrder(-900)]
public class UViperB : MonoBehaviour, IAdvance
{
    private Viper viper;
    public GameObject LF, RF, LB, RB;

    static ChContactMaterial CustomWheelMaterial(ChContactMethod contact_method)
    {
        float mu = 0.8f;
        float cr = 0.1f;
        float Y = 2e7f;
        float nu = 0.3f;
        float kn = 2e5f;
        float gn = 40.0f;
        float kt = 2e5f;
        float gt = 20.0f;

        if (contact_method == ChContactMethod.NSC)
        {
            var matNSC = new ChContactMaterialNSC();
            matNSC.SetFriction(mu);
            matNSC.SetRestitution(cr);
            return matNSC;
        }
        else // SMC
        {
            var matSMC = new ChContactMaterialSMC();
            matSMC.SetFriction(mu);
            matSMC.SetRestitution(cr);
            matSMC.SetYoungModulus(Y);
            matSMC.SetPoissonRatio(nu);
            matSMC.SetKn(kn);
            matSMC.SetGn(gn);
            matSMC.SetKt(kt);
            matSMC.SetGt(gt);
            return matSMC;
        }
    }

    void Start()
    {
        UChSystem system = UnityEngine.Object.FindFirstObjectByType<UChSystem>();
        system.Register(gameObject.name + "_viper", this);
        // ViperSpeedDriver driver = new ViperSpeedDriver(0.1, 2.0);
        viper = new Viper(UChSystem.chrono_system, ViperWheelType.CustomizedWheel, "cobra_wheel_v2");
        ViperSpeedDriver driver = new ViperSpeedDriver(0.1, 2.0);
        viper.SetDriver(driver);
        viper.SetWheelContactMaterial(CustomWheelMaterial(ChContactMethod.NSC));
        ChQuaterniond qAxis = new ChQuaterniond(chrono.QuatFromAngleX( -chrono.CH_PI_2)); // Rotate Viper from Z-up to Y-up
        viper.Initialize(new ChFramed(
             Utils.ToChronoFlip(transform.position),
             Utils.ToChronoFlip(transform.rotation).__rmul__(qAxis))
        );

        // Get VIPER wheels and chassis body to set up SCM patches
        var wheel_LF = viper.GetWheel(ViperWheelID.V_LF).GetBody();
        var wheel_RF = viper.GetWheel(ViperWheelID.V_RF).GetBody();
        var wheel_LB = viper.GetWheel(ViperWheelID.V_LB).GetBody();
        var wheel_RB = viper.GetWheel(ViperWheelID.V_RB).GetBody();
        var viper_body = viper.GetChassis().GetBody();

        var wheel_mat = new ChVisualMaterial();
        wheel_mat.SetDiffuseColor(new ChColor(2f, 2f, 2f));
        wheel_mat.SetSpecularColor(new ChColor(2f, 2f, 2f));
        wheel_mat.SetUseSpecularWorkflow(true);
        wheel_mat.SetMetallic(0f);
        wheel_mat.SetRoughness(1.0f);
        wheel_LB.GetVisualShape(0).SetMaterial(0, wheel_mat);
        wheel_LB.GetVisualShape(0).GetMaterial(0).SetClassID(0);
        wheel_LB.GetVisualShape(0).GetMaterial(0).SetInstanceID(65535);
    }

    void Awake()
    {
    }

    public Viper GetViper()
    {
        return viper;
    }

    public void Advance(double step)
    {
        if (viper != null)
        {
            var pos = viper.GetChassisPos();
            var rot = viper.GetChassisRot();

        
            transform.position = Utils.FromChronoFlip(pos);
            ChQuaterniond qAxis = new ChQuaterniond(chrono.QuatFromAngleX( chrono.CH_PI_2)); // Rotate Viper from Z-up to Y-up
            transform.rotation = Utils.FromChronoFlip(rot.__rmul__(qAxis));


            LF.transform.position = Utils.FromChronoFlip(viper.GetWheel(ViperWheelID.V_LF).GetPos());
            LF.transform.rotation = Utils.FromChronoFlip(viper.GetWheel(ViperWheelID.V_LF).GetRot().__rmul__(qAxis));

            RF.transform.position = Utils.FromChronoFlip(viper.GetWheel(ViperWheelID.V_RF).GetPos());
            RF.transform.rotation = Utils.FromChronoFlip(viper.GetWheel(ViperWheelID.V_RF).GetRot().__rmul__(qAxis));

            LB.transform.position = Utils.FromChronoFlip(viper.GetWheel(ViperWheelID.V_LB).GetPos());
            LB.transform.rotation = Utils.FromChronoFlip(viper.GetWheel(ViperWheelID.V_LB).GetRot().__rmul__(qAxis));

            RB.transform.position = Utils.FromChronoFlip(viper.GetWheel(ViperWheelID.V_RB).GetPos());
            RB.transform.rotation = Utils.FromChronoFlip(viper.GetWheel(ViperWheelID.V_RB).GetRot().__rmul__(qAxis));

            viper.Update();

        }
    }

}

