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
// Custom vehicle inspector for UWheeledVehicle - uses tab system to pull
// chrono jsons and develop/edit/create a vehicle. Allows for easy editing and
// switching and handling json data
// =============================================================================

using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;

[CustomEditor(typeof(UChSegmentationCamera))]
public class UChSegmentationCameraInspector : UChSensorInspector
{
	// Serialized properties
	private SerializedProperty bodySourceProp;
	private SerializedProperty updateRateProp;
	private SerializedProperty widthProp;
	private SerializedProperty heightProp;
	private SerializedProperty hFovProp;
	private SerializedProperty lensModelProp;

	// UI state
	private bool showAcquisition = true;
	private bool showRendering = true;


	private static readonly GUIContent BodySourceLabel = new GUIContent("Body Source", "Vehicle or rover providing the Chrono body for the sensor.");
	private static readonly GUIContent UpdateRateLabel = new GUIContent("Update Rate (Hz)", "Sensor update frequency in Hertz.");
	private static readonly GUIContent ResolutionLabel = new GUIContent("Resolution", "Image width and height in pixels.");
	private static readonly GUIContent[] ResolutionSubLabels = 
			{
				new GUIContent("W", "Image width in pixels."),
				new GUIContent("H", "Image height in pixels."),
			};

	private static readonly GUIContent ResolutionWidthLabel = new GUIContent("W", "Image width in pixels.");
	private static readonly GUIContent ResolutionHeightLabel = new GUIContent("H", "Image height in pixels.");
	private static readonly GUIContent HFovLabel = new GUIContent("Horizontal FOV", "Horizontal field of view in radians.");
	private static readonly GUIContent LensLabel = new GUIContent("Lens Model", "Camera lens model used by the Chrono sensor.");

	protected override void OnEnable()
	{
		bodySourceProp = serializedObject.FindProperty("bodySource");
		updateRateProp = serializedObject.FindProperty("updateRate");
		widthProp = serializedObject.FindProperty("w");
		heightProp = serializedObject.FindProperty("h");
		hFovProp = serializedObject.FindProperty("hFOV");
		lensModelProp = serializedObject.FindProperty("lensModel");
		base.OnEnable();
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();

		DrawBodySourceSection();
		EditorGUILayout.Space();

		DrawAcquisitionSection();
		EditorGUILayout.Space();

		DrawRenderingSection();
		EditorGUILayout.Space();

		DrawFilterSection();
		EditorGUILayout.Space();

		DrawValidationMessages();

		serializedObject.ApplyModifiedProperties();
	}

	private void DrawBodySourceSection()
	{
		EditorGUILayout.LabelField("Attachment", EditorStyles.boldLabel);

		var parent = GetParentGameObject();
		using (new EditorGUI.DisabledScope(true))
		{
			EditorGUILayout.ObjectField(BodySourceLabel, parent, typeof(GameObject), true);
		}

		if (parent == null)
		{
			EditorGUILayout.HelpBox("Sensor must be parented under a UChVehicle or UViper. The parent will be used automatically as the body source.", UnityEditor.MessageType.Warning);
			return;
		}

		if (!UChSensor.ParentHasSupportedBody(parent))
		{
			EditorGUILayout.HelpBox("Parent object needs a UChVehicle or UViper or UChBody component to host the sensor.", UnityEditor.MessageType.Error);
		}
	}

	private GameObject GetParentGameObject()
	{
		var sensor = (UChSegmentationCamera)target;
		return sensor.transform.parent ? sensor.transform.parent.gameObject : null;
	}

	private void DrawAcquisitionSection()
	{
		showAcquisition = EditorGUILayout.BeginFoldoutHeaderGroup(showAcquisition, "Acquisition Parameters");
		if (showAcquisition)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(updateRateProp, UpdateRateLabel);
			updateRateProp.floatValue = Mathf.Max(0.01f, updateRateProp.floatValue);

			int[] res =
			{
				(int)widthProp.longValue,
				(int)heightProp.longValue
			};

			Rect r = EditorGUILayout.GetControlRect();

			r = EditorGUI.PrefixLabel(r, ResolutionLabel);

			int oldIndent = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;


			EditorGUI.MultiIntField(r, ResolutionSubLabels, res);

			EditorGUI.indentLevel = oldIndent;

			widthProp.longValue = Math.Max(1, res[0]);
			heightProp.longValue = Math.Max(1, res[1]);

			EditorGUILayout.PropertyField(hFovProp, HFovLabel);
			hFovProp.floatValue = Mathf.Clamp(hFovProp.floatValue, 0.01f, Mathf.PI);

			EditorGUI.indentLevel--;
		}
		EditorGUILayout.EndFoldoutHeaderGroup();
	}

	private void DrawRenderingSection()
	{
		showRendering = EditorGUILayout.BeginFoldoutHeaderGroup(showRendering, "Rendering & Post");
		if (showRendering)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(lensModelProp, LensLabel);

			EditorGUI.indentLevel--;
		}
		EditorGUILayout.EndFoldoutHeaderGroup();
	}

	private void DrawValidationMessages()
	{
		if (widthProp.longValue % 2 != 0 || heightProp.longValue % 2 != 0)
		{
			EditorGUILayout.HelpBox("For best GPU performance, prefer even resolution values.", UnityEditor.MessageType.Info);
		}

	}

}
