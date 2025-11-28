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

[CustomEditor(typeof(UChSensor))]
public class UChSensorInspector : Editor
{
	// Serialized properties
	private SerializedProperty filtersProp;

	// UI state
	private bool showFilters = true;

	private ReorderableList filterList;
	private readonly List<Type> availableFilterTypes = new List<Type>();

	protected virtual void OnEnable()
	{
		filtersProp = serializedObject.FindProperty("Filters");

		CacheFilterTypes();
		BuildFilterList();
	}

	public override void OnInspectorGUI()
	{
	}


	protected void DrawFilterSection()
	{
		showFilters = EditorGUILayout.BeginFoldoutHeaderGroup(showFilters, "Filters");
		if (showFilters)
		{
			EditorGUI.indentLevel++;
			if (filterList != null)
			{
				filterList.DoLayoutList();
			}
			else
			{
				EditorGUILayout.HelpBox("Filter list is not available. Try reselecting the object.", MessageType.Warning);
			}
			EditorGUI.indentLevel--;
		}
		EditorGUILayout.EndFoldoutHeaderGroup();
	}

	private void CacheFilterTypes()
	{
		availableFilterTypes.Clear();
		foreach (var type in TypeCache.GetTypesDerivedFrom<UChFilter>())
		{
			if (type.IsAbstract || type.ContainsGenericParameters)
			{
				continue;
			}

			if (!typeof(MonoBehaviour).IsAssignableFrom(type))
			{
				continue;
			}

			availableFilterTypes.Add(type);
		}
		availableFilterTypes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
	}

	private void BuildFilterList()
	{
		if (filtersProp == null)
		{
			return;
		}

		filterList = new ReorderableList(serializedObject, filtersProp, true, true, true, true)
		{
			drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Sensor Filters"),
			drawElementCallback = DrawFilterElement,
			onAddDropdownCallback = HandleAddFilter,
			onRemoveCallback = list => RemoveFilterAt(list.index),
			elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2f
		};
	}

	private void DrawFilterElement(Rect rect, int index, bool isActive, bool isFocused)
	{
		if (filtersProp == null || index < 0 || index >= filtersProp.arraySize)
		{
			return;
		}

		SerializedProperty element = filtersProp.GetArrayElementAtIndex(index);
		rect.height = EditorGUIUtility.singleLineHeight;
		rect.y += 2;
		var filterObj = element.objectReferenceValue as UChFilter;

		string label = "None";
		if (filterObj != null)
		{
			label = filterObj.FilterName;
		}

		EditorGUI.PropertyField(rect, element, new GUIContent(label));
	}

	private void HandleAddFilter(Rect buttonRect, ReorderableList list)
	{
		var menu = new GenericMenu();
		if (availableFilterTypes.Count == 0)
		{
			menu.AddDisabledItem(new GUIContent("No UChFilter implementations found"));
		}
		else
		{
			foreach (var type in availableFilterTypes)
			{
				var capturedType = type;
				menu.AddItem(new GUIContent(capturedType.Name), false, () => AddFilterToSelection(capturedType));
			}
		}
		menu.ShowAsContext();
	}

	private void AddFilterToSelection(Type filterType)
	{
		foreach (UnityEngine.Object obj in targets)
		{
			if (obj is not UChCameraSensor sensor)
			{
				continue;
			}

			Undo.RegisterCompleteObjectUndo(sensor, "Add Sensor Filter");
			var newFilter = Undo.AddComponent(sensor.gameObject, filterType) as UChFilter;
			if (newFilter == null)
			{
				continue;
			}

			if (sensor.Filters == null)
			{
				sensor.Filters = new List<UChFilter>();
			}

			if (!sensor.Filters.Contains(newFilter))
			{
				sensor.Filters.Add(newFilter);
			}
			EditorUtility.SetDirty(sensor);
		}

		serializedObject.Update();
	}

	private void RemoveFilterAt(int index)
	{
		if (filtersProp == null || index < 0 || index >= filtersProp.arraySize)
		{
			return;
		}

		SerializedProperty element = filtersProp.GetArrayElementAtIndex(index);
		var filter = element.objectReferenceValue as UChFilter;

		filtersProp.DeleteArrayElementAtIndex(index);
		if (index < filtersProp.arraySize)
		{
			SerializedProperty maybeNull = filtersProp.GetArrayElementAtIndex(index);
			if (maybeNull != null && maybeNull.objectReferenceValue == null)
			{
				filtersProp.DeleteArrayElementAtIndex(index);
			}
		}
		serializedObject.ApplyModifiedProperties();

		if (filter != null)
		{
			Undo.DestroyObjectImmediate(filter);
		}

		serializedObject.Update();
	}
}
