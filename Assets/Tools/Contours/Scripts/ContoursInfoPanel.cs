﻿// Copyright (C) 2019 Singapore ETH Centre, Future Cities Laboratory
// All rights reserved.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// Author:  David Neudecker  (neudecker@arch.ethz.ch)
//          Muhammad Salihin Bin Zaol-kefli

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class ContoursInfoPanel : MonoBehaviour, IOutput
{
	private class InfoPanelEntry
	{
		public KeyValuePair pair;
		public double? sqm = null;
	}

	[Header("UI References")]
	public Dropdown unitsDropdown;
	public GameObject message;
	public GameObject stats;
	public Transform container;
	public Text note;

	[Header("Prefabs")]
	public KeyValuePair itemPrefab;

	[Header("Settings")]
	public AreaUnit[] units = new AreaUnit[]
	{
		new AreaUnit
		{
			name = "Hectares"/*translatable*/,
			symbol = "ha",
			factor = 0.0001
		},
		new AreaUnit
		{
			name = "Square Kilometers"/*translatable*/,
			symbol = "km\u00B2",
			factor = 0.000001
		},
		new AreaUnit
		{
			name = "Square Meters"/*translatable*/,
			symbol = "m\u00B2",
			factor = 1
		}
	};


	private readonly Dictionary<string, InfoPanelEntry> entries = new Dictionary<string, InfoPanelEntry>();
	private AreaUnit selectedUnit = null;

	//
	// Unity Methods
	//

	private void Awake()
	{
		LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
		UpdateNote();
	}

	public void Init()
	{
		unitsDropdown.ClearOptions();
		if (units.Length > 0)
		{
			foreach (var unit in units)
			{
				unitsDropdown.options.Add(new Dropdown.OptionData(Translator.Get(unit.name) + " (" + unit.symbol + ")"));
			}
			selectedUnit = units[0];
		}

		unitsDropdown.onValueChanged.RemoveListener(OnUnitsChanged);
		unitsDropdown.onValueChanged.AddListener(OnUnitsChanged);

		ShowStats(false);
	}

	private void OnDestroy()
	{
		LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
	}

	//
	// Event Methods
	//

	private void OnUnitsChanged(int value)
	{
		selectedUnit = units[value];
		UpdateAll();
	}

	private void OnLanguageChanged()
	{
		UpdateNote();
		UpdateDropdown();

		// Retranslate the "N/A"
		foreach (var entry in entries.Values)
		{
			if (entry.pair.Disabled)
				entry.pair.SetValue(Translator.Get("N/A"));
		}
	}


	//
	// Public Methods
	//

	public void ShowStats(bool show)
	{
		message.SetActive(!show);
		stats.SetActive(show);
	}

	public void AddEntry(string id, string keylabel)
    {
		// Hide "no data layers" message
		message.SetActive(false);

        var pair = Instantiate(itemPrefab, container, false);
		pair.SetKey(keylabel);

		var entry = new InfoPanelEntry
		{
			pair = pair,
		};
		entries.Add(id, entry);

		UpdateEntryValue(entry);
    }

	// Called after renaming a snapshot
	public void RenameEntry(string id, string keyLabel)
	{
		if (!entries.TryGetValue(id, out InfoPanelEntry entry))
		{
			Debug.LogError("Entry " + id + " could not be renamed: not found");
			return;
		}

		entry.pair.SetKey(keyLabel);
	}

	public void UpdateEntry(string id, double sqm)
    {
        if (entries.TryGetValue(id, out InfoPanelEntry entry))
        {
			entry.sqm = sqm;
			UpdateEntryValue(entry);
		}
    }

	public void ClearEntry(string id)
	{
		if (entries.TryGetValue(id, out InfoPanelEntry entry))
		{
			entry.sqm = null;
			ClearEntry(entry);
		}
	}

	public void ClearAll()
	{
		foreach (var entry in entries.Values)
		{
			entry.sqm = null;
			ClearEntry(entry);
		}
	}

	public void OutputToCSV(TextWriter csv)
	{
		foreach (var entry in entries)
		{
			WriteContourInfo(entry.Value, csv);
		}

        // Export only when there is a selected contour
        var contoursTool = ComponentManager.Instance.Get<ContoursTool>();
        if (contoursTool.ContoursLayer.SelectedContour > 1)
        {
            string exportPath = "";

#if !UNITY_WEBGL || UNITY_EDITOR
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
		    exportPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            exportPath += Path.DirectorySeparatorChar + "ur-scape ";
#else
            exportPath = System.Environment.CurrentDirectory + Path.DirectorySeparatorChar; // Export to where the executable is
#endif
            exportPath += "Export" + Path.DirectorySeparatorChar;

            // Create export directory
            Directory.CreateDirectory(exportPath);
#endif

            // Add the date as a file prefix
#if UNITY_WEBGL
            var filesPath = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_");
#else
            var filesPath = exportPath + System.DateTime.Now.ToString("yyyyMMdd_HHmmss_");
#endif
            WriteSelectedContourInfo(filesPath + "SelectedContour.csv");
        }
	}


	//
	// Private Methods
	//

	private void ClearEntry(InfoPanelEntry entry)
	{
		entry.pair.SetValue(Translator.Get("N/A"));
		if (!entry.pair.Disabled)
			entry.pair.Disabled = true;
	}

	private void UpdateAll()
    {
		foreach (var entry in entries.Values)
        {
			UpdateEntryValue(entry);
		}
	}

    private void UpdateEntryValue(InfoPanelEntry entry)
    {
		if (!entry.sqm.HasValue)
		{
			ClearEntry(entry);
			return;
		}

		var sufix = "";
		float area = (float)(entry.sqm * selectedUnit.factor);
		if (area > 1e+12)
		{
			area *= 1e-12f;
			sufix = Translator.Get("trillion");
		}
		else if (area > 1e+9)
		{
			area *= 1e-9f;
			sufix = Translator.Get("billion");
		}
		else if (area > 1e+6)
		{
			area *= 1e-6f;
			sufix = Translator.Get("million");
		}

		entry.pair.SetValue(area.ToString("N") + " " + sufix + " " + selectedUnit.symbol);
		if (entry.pair.Disabled)
			entry.pair.Disabled = false;
	}

	private void WriteContourInfo(InfoPanelEntry entry, TextWriter csv)
    {
		string value = entry.pair.GetValue();
        csv.WriteLine("{0},{1}", entry.pair.GetKey().ToUpper(), CsvHelper.Escape(value));
    }

    private void WriteSelectedContourInfo(string filename)
    {
        var contoursTool = ComponentManager.Instance.Get<ContoursTool>();
        var dataLayers = ComponentManager.Instance.Get<DataLayers>();
		var translator = LocalizationManager.Instance;

        var contourGrids = contoursTool.ContoursLayer.grids;
        var contourInspectedGrids = contoursTool.ContoursLayer.inspectedGridsData;

        int contourGridsLength = contourGrids.Count;

        Dictionary<string, string> lines = new Dictionary<string, string>();

        using (var memStream = new MemoryStream())
		{
			using (var csv = new StreamWriter(memStream, System.Text.Encoding.UTF8))
            {
                csv.Write("{0},", translator.Get("Cells"));
                for (int i = 0; i < contourGridsLength; ++i)
                {
                    var layerName = dataLayers.activeLayerPanels[i].name;
                    int contourInspectedGridsLength = contourInspectedGrids[contourGrids[i]].Count;
                    string units = contourGrids[i].units;

                    csv.Write("{0},", $"{translator.Get(layerName)} ({translator.Get(units)})");

                    for (int j = 0; j < contourInspectedGridsLength; ++j)
                    {
                        string index = (j + 1).ToString();
                        float value = contourInspectedGrids[contourGrids[i]][j];

                        if (!lines.ContainsKey(index))
                        {
                            lines.Add(index, value.ToString());
                        }
                        else
                        {
                            lines[index] += $",{value}";
                        }
                    }
                }

                csv.WriteLine();
                foreach (var line in lines)
                {
                    var lineToWrite = $"{line.Key},{line.Value}";
                    csv.WriteLine(lineToWrite);
                }
            }
            var exportTool = ComponentManager.Instance.Get<ExportTool>();
            exportTool.WriteFile(filename, memStream.GetBuffer());
        }
    }

	private void UpdateNote()
	{
		var translator = LocalizationManager.Instance;
		note.text = translator.Get("Note") + ":  " + translator.Get("billion") + " = 10<size=16>\u2079</size>,  " + translator.Get("trillion") + " = 10<size=16>\xB9\xB2</size>";
	}

	private void UpdateDropdown()
	{
		for (int i = 0; i < units.Length; i++)
		{
			unitsDropdown.options[i].text = Translator.Get(units[i].name) + " (" + units[i].symbol + ")";
		}
		unitsDropdown.captionText.text = unitsDropdown.options[unitsDropdown.value].text;
	}

}
