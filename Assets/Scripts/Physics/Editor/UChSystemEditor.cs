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
// Authors: Radu Serban
// =============================================================================

using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(UChSystem))]
public class UChSystemEditor : Editor
{
    override public void OnInspectorGUI()
    {
        UChSystem sys = (UChSystem)target;

        sys.step = EditorGUILayout.DoubleField("Step", sys.step);
        sys.gravity = EditorGUILayout.Vector3Field("Gravity", sys.gravity);

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Contact method options

        string[] options = new string[] { "NSC", "SCM" };
        sys.contact_method = (ChContactMethod)EditorGUILayout.Popup("Contact Method", (int)sys.contact_method, options, EditorStyles.popup);

        if (sys.contact_method == ChContactMethod.SMC)
        {
            string[] force_options = new string[] { "Hooke", "Hertz", "PlainCoulomb" };  // "Flores"
            string[] adhesion_options = new string[] { "Constant", "DMT" }; // "Perko"
            string[] tdispl_options = new string[] { "None", "OneStep", "MultiStep" };
            //EditorGUI.indentLevel++;
            sys.contactModel = (ChSystemSMC.ContactForceModel)EditorGUILayout.Popup("Force Model", (int)sys.contactModel, force_options, EditorStyles.popup);
            sys.adhesionModel = (ChSystemSMC.AdhesionForceModel)EditorGUILayout.Popup("Adhesion Model", (int)sys.adhesionModel, adhesion_options, EditorStyles.popup);
            sys.tdisplModel = (ChSystemSMC.TangentialDisplacementModel)EditorGUILayout.Popup("Tangential Model", (int)sys.tdisplModel, tdispl_options, EditorStyles.popup);
            sys.useMatProps = EditorGUILayout.Toggle("Use Mat. Props.", sys.useMatProps);
            //EditorGUI.indentLevel--;
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Solver options

        string[] solver_options;
        if (sys.contact_method == ChContactMethod.NSC)
            solver_options = new string[] { "PSOR", "BARZILAIBORWEIN", "APGD" };
        else
            solver_options = new string[] { "PSOR", "BARZILAIBORWEIN", "APGD",
                                            "SPARSE_LU", "SPARSE_QR", "GMRES", "MINRES" };
        sys.solverType = (UChSystem.SolverType)EditorGUILayout.Popup("Solver Type", (int)sys.solverType, solver_options, EditorStyles.popup);

        if (sys.solverType == UChSystem.SolverType.SPARSE_LU || sys.solverType == UChSystem.SolverType.SPARSE_QR)
        {
            sys.solverUseLearner = EditorGUILayout.Toggle("Sparsity Learner", sys.solverUseLearner);
            sys.solverLockSparsityPattern = EditorGUILayout.Toggle("Lock Sparsity", sys.solverLockSparsityPattern);
            sys.solverSparsityEstimate = EditorGUILayout.DoubleField("Sparsity Estimate", sys.solverSparsityEstimate);
        }
        else
        {
            sys.solverMaxIterations = EditorGUILayout.IntField("Max. Iterations", sys.solverMaxIterations);
            sys.solverTolerance = EditorGUILayout.DoubleField("Tolerance", sys.solverTolerance);
            sys.solverEnablePrecond = EditorGUILayout.Toggle("Diag. Preconditioner", sys.solverEnablePrecond);
            sys.solverEnableWarmStart = EditorGUILayout.Toggle("Warm Start", sys.solverEnableWarmStart);
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Integrator options
        // NSC (DVI): All implicit Euler variants work with VI solvers (PSOR, APGD, BB)
        //            EULER_IMPLICIT assembles a complementarity problem each Newton iteration for the VI solver
        //            HHT cannot be used with NSC/DVI contacts
        // SMC: HHT requires direct/LS solvers (SPARSE_LU, SPARSE_QR, GMRES, MINRES)

        string[] integrator_options;
        bool isDirectSolver = (sys.solverType == UChSystem.SolverType.SPARSE_LU || 
                               sys.solverType == UChSystem.SolverType.SPARSE_QR ||
                               sys.solverType == UChSystem.SolverType.GMRES || 
                               sys.solverType == UChSystem.SolverType.MINRES);

        if (sys.contact_method == ChContactMethod.NSC)
        {
            // NSC: All implicit Euler variants work with VI solvers, but no HHT
            integrator_options = new string[] { "EULER_IMPLICIT_LINEARIZED", "EULER_IMPLICIT_PROJECTED", "EULER_IMPLICIT" };
        }
        else
        {
            // SMC: All integrators available, HHT typically used with direct solvers
            if (isDirectSolver)
                integrator_options = new string[] { "EULER_IMPLICIT_LINEARIZED", "EULER_IMPLICIT_PROJECTED", "EULER_IMPLICIT", "HHT" };
            else
                integrator_options = new string[] { "EULER_IMPLICIT_LINEARIZED", "EULER_IMPLICIT_PROJECTED", "EULER_IMPLICIT" };
        }

        sys.integratorType = (UChSystem.IntegratorType)EditorGUILayout.Popup("Integrator Type", (int)sys.integratorType, integrator_options, EditorStyles.popup);
        
        // Clamp integrator type if current selection is not in available options
        if ((int)sys.integratorType >= integrator_options.Length)
        {
            sys.integratorType = UChSystem.IntegratorType.EULER_IMPLICIT_LINEARIZED;
        }

        if (sys.integratorType == UChSystem.IntegratorType.EULER_IMPLICIT || sys.integratorType == UChSystem.IntegratorType.HHT)
        {
            sys.integratorRelTol = EditorGUILayout.DoubleField("Rel. Tol.", sys.integratorRelTol);
            sys.integratorAbsTolS = EditorGUILayout.DoubleField("Abs. Tol. States", sys.integratorAbsTolS);
            sys.integratorAbsTolL = EditorGUILayout.DoubleField("Abs. Tol. Multipliers", sys.integratorAbsTolL);
            sys.integratorMaxIters = EditorGUILayout.IntField("Max. N-R Iterations", sys.integratorMaxIters);
        }

        if (sys.integratorType == UChSystem.IntegratorType.HHT)
        {
            sys.hhtAlpha = EditorGUILayout.DoubleField("HHT Alpha", sys.hhtAlpha);
            sys.hhtScaling = EditorGUILayout.Toggle("HHT Scaling", sys.hhtScaling);
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(sys);
        }
    }
}
