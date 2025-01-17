﻿// Copyright (C) 2019 Singapore ETH Centre, Future Cities Laboratory
// All rights reserved.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// Author:  Muhammad Salihin Bin Zaol-kefli

using System;
using UnityEngine;
using UnityEngine.UI;

public class CellInspectorPanel : MonoBehaviour
{
	[Header("Prefabs")]
	public Graphic dotPrefab;
	public Text labelPrefab;
	public Text valuePrefab;

	[Header("UI References")]
	public RectTransform container;

	private MapController map;
	private DataLayers dataLayers;
	private InputHandler inputHandler;


	//
	// Unity Methods
	//

	private void OnEnable()
	{
		map = ComponentManager.Instance.Get<MapController>();
		dataLayers = ComponentManager.Instance.Get<DataLayers>();
		inputHandler = ComponentManager.Instance.Get<InputHandler>();
	}


	//
	// Public Methods
	//

	public void UpdateValues()
	{
		if (!inputHandler.GetWorldPoint(Input.mousePosition, out Vector3 worldPos))
			return;

		Coordinate coords = map.GetCoordinatesFromUnits(worldPos.x, worldPos.z);

		int index = 0;
		foreach (var layerPanel in dataLayers.activeLayerPanels)
		{
			var layer = layerPanel.DataLayer;
			if (!dataLayers.availableLayers.Contains(layer))
				continue;

			string layerSufix = "";
			string cellValue = "N/A";

			foreach (var patch in layer.loadedPatchesInView)
			{
				if (patch is GridPatch gridPatch)
				{
					if (GetGridDataValue(gridPatch.grid, coords, true, out cellValue))
						break;
				}
				else if (patch is MultiGridPatch multiPatch)
				{
					var multiGrid = multiPatch.multigrid;
					if (multiGrid.IsInside(coords.Longitude, coords.Latitude))
					{
						var categories = multiGrid.categories;
						if (categories != null && categories.Length > 0)
						{
							int last = categories.Length - 1;
							for (int i = 0; i < last; i++)
							{
								layerSufix = " - " + categories[i].name;
								GetGridDataValue(categories[i].grid, coords, false, out cellValue);
								UpdateRow(index++, layer.Name, layerSufix, layer.Color, cellValue);
							}

							if (last >= 0)
							{
								layerSufix = " - " + categories[last].name;
								GetGridDataValue(categories[last].grid, coords, false, out cellValue);
							}

							break;
						}
					}
				}
				else if (patch is GraphPatch graphPatch)
				{
					if (GetGridDataValue(graphPatch, coords, out cellValue))
						break;
				}
			}

			// Set (or add) a row
			UpdateRow(index++, layer.Name, layerSufix, layer.Color, cellValue);
		}

#if UNITY_EDITOR
		// Also add the tool grids
		var toolLayers = map.GetLayerController<ToolLayerController>();
		foreach (var mapLayer in toolLayers.mapLayers)
		{
			string cellValue = "N/A";
			var grid = mapLayer.Grid;
			if (grid is GridData gridData)
			{
				if (GetGridDataValue(gridData, coords, true, out cellValue))
					UpdateRow(index++, mapLayer.name, "", Color.gray, cellValue);
			}
		}
#endif

		// Remove remaining rows
		RemoveRemainingItems(index);
	}


	//
	// Private Methods
	//

	private bool GetGridDataValue(GridData gridData, Coordinate coords, bool units, out string cellValue)
	{
		if (gridData.values != null && gridData.IsInside(coords.Longitude, coords.Latitude))
		{
			float? cell;
			if (gridData.valuesMask == null)
				cell = gridData.GetValue(coords.Longitude, coords.Latitude);
			else
				cell = gridData.GetCell(coords.Longitude, coords.Latitude);

			if (cell.HasValue)
			{
				if (gridData.IsCategorized)
				{
					int category = Mathf.RoundToInt(cell.Value);
					if (category >= 0 && category < gridData.categories.Length)
					{
						cellValue = gridData.categories[category].name;
						return true;
					}
					else
					{
						cellValue = "Unknown (" + category + ")";
						return true;
					}
				}
				else
				{
					cellValue = cell.Value.ToString("#,##0.####");
					if (units)
						cellValue += " " + gridData.units;
					return true;
				}
			}
		}
		cellValue = "N/A";
		return false;
	}

	private bool GetGridDataValue(GraphPatch graphPatch, Coordinate coords, out string cellValue)
	{
		var gridData = graphPatch.grid;
		if (gridData.values != null && gridData.IsInside(coords.Longitude, coords.Latitude))
		{
			int classification = (int)gridData.GetValue(coords.Longitude, coords.Latitude);
			if (classification == 0)
			{
				cellValue = "None";
			}
			else
			{
				cellValue = "";
				var classificationNames = Enum.GetNames(typeof(ClassificationIndex));
				for (int i = 1, k = 1; i < (int)ClassificationIndex.Count; i++, k*=2)
				{
					if ((classification & k) != 0)
						cellValue += classificationNames[i] + ", ";
				}
				cellValue = cellValue.Remove(cellValue.Length - 2);
			}
			return true;
		}
		cellValue = "N/A";
		return false;
	}

	private void UpdateRow(int index, string layerName, string layerSufix, Color color, string cellValue)
	{
		Graphic dot;
		Text label, value;
		int containerIndex = index * 3;

		if (containerIndex >= container.childCount)
		{
			// Instantiate prefabs
			dot = Instantiate(dotPrefab, container, false);
			label = Instantiate(labelPrefab, container, false);
			value = Instantiate(valuePrefab, container, false);
		}
		else
		{
			// Get existing components
			dot = container.GetChild(containerIndex++).GetComponent<Graphic>();
			label = container.GetChild(containerIndex++).GetComponent<Text>();
			value = container.GetChild(containerIndex++).GetComponent<Text>();
		}

		// Update values
		dot.color = color;
		label.text = layerName + layerSufix;
		value.text = cellValue;
	}

	private void RemoveRemainingItems(int start = 0)
	{
		start *= 3;
		for (int i = container.childCount - 1; i >= start; i--)
		{
			Destroy(container.GetChild(i).gameObject);
		}
	}

}
