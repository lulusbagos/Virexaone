using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Virexa.FMS
{
    public class FMSUnitAssetManager : MonoBehaviour
    {
        public static FMSUnitAssetManager Instance { get; private set; }

        public enum UnitCategory
        {
            HaulerEmpty,
            HaulerLoaded,
            Excavator,
            Bulldozer,
            Grader,
            FuelTruck,
            WheelLoader,
            Support
        }

        [System.Serializable]
        public class UnitModelConfig
        {
            public UnitCategory category;
            public string displayName;
            public string icon;
            public string defaultGlbFile;
            public string assignedFilePath;
            public float scaleMultiplier = 1.0f;
            public float actualLengthMeters = 10.8f;
            public float actualWidthMeters = 5.8f;
            public float actualHeightMeters = 5.3f;
            public string realMachineReference = "CAT 777E / HD785";
            public bool isLoaded = true;
        }

        [System.Serializable]
        public class DimensionsDto
        {
            public float length;
            public float width;
            public float height;
        }

        [System.Serializable]
        public class UnitAssetJsonEntry
        {
            public string category;
            public string display_name;
            public string icon;
            public string model_file_name;
            public string relative_path;
            public string file_format;
            public float scale_multiplier;
            public DimensionsDto actual_dimensions_m;
            public string reference_machine;
            public bool is_active;
        }

        [System.Serializable]
        public class AppSettingsJsonWrapper
        {
            public string version;
            public string updated_at;
            public List<UnitAssetJsonEntry> unit_assets_table;
        }

        public List<UnitModelConfig> configs = new List<UnitModelConfig>();

        private const string PREF_PREFIX = "Virexa_UnitModel_";
        private const string DEFAULT_SOURCE_DIR = @"D:\4. PROJECT\20. Astha\Unit\Asset full GLB";
        private string ConfigFilePath => Path.Combine(Application.dataPath, "Data", "fms_app_settings.json");

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            InitDefaultConfigs();
            LoadSavedSettings();
        }

        private void InitDefaultConfigs()
        {
            if (configs.Count > 0) return;

            configs.Add(new UnitModelConfig
            {
                category = UnitCategory.HaulerEmpty,
                displayName = "Dump Truck (Kosong / Traveling)",
                icon = "🚚",
                defaultGlbFile = "truck_empty_draco.glb",
                assignedFilePath = "Assets/Models/UnitModels/truck_empty_draco.glb",
                scaleMultiplier = 1.0f,
                actualLengthMeters = 10.8f,
                actualWidthMeters = 5.8f,
                actualHeightMeters = 5.3f,
                realMachineReference = "CAT 777D/E / Komatsu HD785 (100-Ton Hauler)",
                isLoaded = true
            });

            configs.Add(new UnitModelConfig
            {
                category = UnitCategory.HaulerLoaded,
                displayName = "Dump Truck (Bermuatan / Hauling)",
                icon = "🚚",
                defaultGlbFile = "truck_loaded_draco.glb",
                assignedFilePath = "Assets/Models/UnitModels/truck_loaded_draco.glb",
                scaleMultiplier = 1.0f,
                actualLengthMeters = 10.8f,
                actualWidthMeters = 5.8f,
                actualHeightMeters = 5.3f,
                realMachineReference = "CAT 777D/E / Komatsu HD785 (Loaded OB/Coal)",
                isLoaded = true
            });

            configs.Add(new UnitModelConfig
            {
                category = UnitCategory.Excavator,
                displayName = "Excavator Shovel (Front Loading)",
                icon = "⛏️",
                defaultGlbFile = "excavator_draco.glb",
                assignedFilePath = "Assets/Models/UnitModels/excavator_draco.glb",
                scaleMultiplier = 1.0f,
                actualLengthMeters = 14.5f,
                actualWidthMeters = 6.8f,
                actualHeightMeters = 6.2f,
                realMachineReference = "Komatsu PC2000-8 / CAT 6020B (Mining Shovel)",
                isLoaded = true
            });

            configs.Add(new UnitModelConfig
            {
                category = UnitCategory.Bulldozer,
                displayName = "Bulldozer (Disposal / Bench)",
                icon = "🚜",
                defaultGlbFile = "dozer_draco.glb",
                assignedFilePath = "Assets/Models/UnitModels/dozer_draco.glb",
                scaleMultiplier = 1.0f,
                actualLengthMeters = 8.8f,
                actualWidthMeters = 4.8f,
                actualHeightMeters = 4.3f,
                realMachineReference = "Komatsu D375A / CAT D9T (Track-type Tractor)",
                isLoaded = true
            });

            configs.Add(new UnitModelConfig
            {
                category = UnitCategory.Grader,
                displayName = "Motor Grader (Perawatan Jalan)",
                icon = "🛣️",
                defaultGlbFile = "grader_draco.glb",
                assignedFilePath = "Assets/Models/UnitModels/grader_draco.glb",
                scaleMultiplier = 1.0f,
                actualLengthMeters = 11.5f,
                actualWidthMeters = 3.4f,
                actualHeightMeters = 3.7f,
                realMachineReference = "CAT 16M / Komatsu GD825A (Haul Road Maintenance)",
                isLoaded = true
            });

            configs.Add(new UnitModelConfig
            {
                category = UnitCategory.FuelTruck,
                displayName = "Fuel & Service Truck (Support)",
                icon = "🛢️",
                defaultGlbFile = "fueltruck_draco.glb",
                assignedFilePath = "Assets/Models/UnitModels/fueltruck_draco.glb",
                scaleMultiplier = 1.0f,
                actualLengthMeters = 9.2f,
                actualWidthMeters = 3.2f,
                actualHeightMeters = 3.5f,
                realMachineReference = "Hino 500 / Scania P380 (Fuel Tanker & Service Rig)",
                isLoaded = true
            });

            configs.Add(new UnitModelConfig
            {
                category = UnitCategory.WheelLoader,
                displayName = "Wheel Loader (Stockpile)",
                icon = "🚜",
                defaultGlbFile = "loader_draco.glb",
                assignedFilePath = "Assets/Models/UnitModels/loader_draco.glb",
                scaleMultiplier = 1.0f,
                actualLengthMeters = 12.5f,
                actualWidthMeters = 4.4f,
                actualHeightMeters = 5.0f,
                realMachineReference = "CAT 992K / Komatsu WA600 (Wheel Loader)",
                isLoaded = true
            });
        }

        public UnitModelConfig GetConfig(UnitCategory cat)
        {
            return configs.Find(c => c.category == cat);
        }

        public UnitModelConfig GetConfigByVehicleType(string unitType, string unitName, string activityName)
        {
            string ut = (unitType ?? "").ToLower();
            string un = (unitName ?? "").ToLower();
            string act = (activityName ?? "").ToLower();

            if (ut.Contains("shovel") || ut.Contains("excavator") || un.StartsWith("ex") || un.StartsWith("pc"))
            {
                return GetConfig(UnitCategory.Excavator);
            }
            if (ut.Contains("dozer") || un.StartsWith("dz") || un.StartsWith("bd"))
            {
                return GetConfig(UnitCategory.Bulldozer);
            }
            if (ut.Contains("grader") || un.StartsWith("mg") || un.StartsWith("gd"))
            {
                return GetConfig(UnitCategory.Grader);
            }
            if (ut.Contains("fuel") || ut.Contains("service") || ut.Contains("support") || un.StartsWith("ft") || un.StartsWith("wt"))
            {
                return GetConfig(UnitCategory.FuelTruck);
            }
            if (ut.Contains("loader") || un.StartsWith("wl"))
            {
                return GetConfig(UnitCategory.WheelLoader);
            }

            if (act.Contains("hauling") || act.Contains("dumping") || act.Contains("loaded"))
            {
                return GetConfig(UnitCategory.HaulerLoaded);
            }
            return GetConfig(UnitCategory.HaulerEmpty);
        }

        public bool AssignCustomModel(UnitCategory cat, string absoluteSourcePath, out string message)
        {
            message = "";
            if (!File.Exists(absoluteSourcePath))
            {
                message = "File tidak ditemukan di disk: " + absoluteSourcePath;
                return false;
            }

            try
            {
                string targetDir = Path.Combine(Application.dataPath, "Models", "UnitModels");
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                string ext = Path.GetExtension(absoluteSourcePath).ToLower();
                string fileName = Path.GetFileName(absoluteSourcePath);
                string targetPath = Path.Combine(targetDir, fileName);

                File.Copy(absoluteSourcePath, targetPath, true);

                var cfg = GetConfig(cat);
                if (cfg != null)
                {
                    cfg.assignedFilePath = $"Assets/Models/UnitModels/{fileName}";
                    cfg.isLoaded = true;
                    SaveSettings();
                }

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
                message = $"Model untuk {cfg?.displayName} berhasil di-update ke '{fileName}'";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Gagal mengimpor model: {ex.Message}";
                return false;
            }
        }

        public void ApplyAllDefaultGlbModels()
        {
            string targetDir = Path.Combine(Application.dataPath, "Models", "UnitModels");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            foreach (var cfg in configs)
            {
                string sourceGlb = Path.Combine(DEFAULT_SOURCE_DIR, cfg.defaultGlbFile);
                string targetPath = Path.Combine(targetDir, cfg.defaultGlbFile);

                if (File.Exists(sourceGlb))
                {
                    try
                    {
                        File.Copy(sourceGlb, targetPath, true);
                        cfg.assignedFilePath = $"Assets/Models/UnitModels/{cfg.defaultGlbFile}";
                        cfg.isLoaded = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FMSUnitAssetManager] Copy error for {cfg.defaultGlbFile}: {ex.Message}");
                    }
                }
            }

            SaveSettings();
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        public void ResetScaleToActual(UnitCategory cat)
        {
            var cfg = GetConfig(cat);
            if (cfg != null)
            {
                cfg.scaleMultiplier = 1.0f;
                SaveSettings();
            }
        }

        public void ResetAllScalesToActual()
        {
            foreach (var cfg in configs)
            {
                cfg.scaleMultiplier = 1.0f;
            }
            SaveSettings();
        }

        public void SaveSettings()
        {
            // 1. Save to PlayerPrefs
            foreach (var cfg in configs)
            {
                PlayerPrefs.SetString(PREF_PREFIX + cfg.category + "_path", cfg.assignedFilePath);
                PlayerPrefs.SetFloat(PREF_PREFIX + cfg.category + "_scale", cfg.scaleMultiplier);
            }
            PlayerPrefs.Save();

            // 2. Persist to clean JSON Table at Assets/Data/fms_app_settings.json
            try
            {
                var list = new List<UnitAssetJsonEntry>();
                foreach (var cfg in configs)
                {
                    list.Add(new UnitAssetJsonEntry
                    {
                        category = cfg.category.ToString(),
                        display_name = cfg.displayName,
                        icon = cfg.icon,
                        model_file_name = Path.GetFileName(cfg.assignedFilePath),
                        relative_path = cfg.assignedFilePath,
                        file_format = cfg.assignedFilePath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) ? "GLB 2.0 Draco PBR" : "3D Mesh",
                        scale_multiplier = cfg.scaleMultiplier,
                        actual_dimensions_m = new DimensionsDto
                        {
                            length = cfg.actualLengthMeters,
                            width = cfg.actualWidthMeters,
                            height = cfg.actualHeightMeters
                        },
                        reference_machine = cfg.realMachineReference,
                        is_active = cfg.isLoaded
                    });
                }

                var wrapper = new AppSettingsJsonWrapper
                {
                    version = "1.0.0",
                    updated_at = DateTime.UtcNow.ToString("o"),
                    unit_assets_table = list
                };

                string json = JsonUtility.ToJson(wrapper, true);
                string jsonDir = Path.GetDirectoryName(ConfigFilePath);
                if (!Directory.Exists(jsonDir)) Directory.CreateDirectory(jsonDir);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FMSUnitAssetManager] JSON save warning: {ex.Message}");
            }
        }

        public void LoadSavedSettings()
        {
            // 1. Load from JSON Table if available
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var wrapper = JsonUtility.FromJson<AppSettingsJsonWrapper>(json);
                    if (wrapper != null && wrapper.unit_assets_table != null)
                    {
                        foreach (var entry in wrapper.unit_assets_table)
                        {
                            if (Enum.TryParse<UnitCategory>(entry.category, out var parsedCat))
                            {
                                var cfg = GetConfig(parsedCat);
                                if (cfg != null)
                                {
                                    if (!string.IsNullOrEmpty(entry.relative_path)) cfg.assignedFilePath = entry.relative_path;
                                    if (entry.scale_multiplier > 0.05f) cfg.scaleMultiplier = entry.scale_multiplier;
                                    if (entry.actual_dimensions_m != null)
                                    {
                                        cfg.actualLengthMeters = entry.actual_dimensions_m.length;
                                        cfg.actualWidthMeters = entry.actual_dimensions_m.width;
                                        cfg.actualHeightMeters = entry.actual_dimensions_m.height;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FMSUnitAssetManager] JSON load notice: {ex.Message}");
            }

            // 2. PlayerPrefs overrides
            foreach (var cfg in configs)
            {
                string pathKey = PREF_PREFIX + cfg.category + "_path";
                string scaleKey = PREF_PREFIX + cfg.category + "_scale";

                if (PlayerPrefs.HasKey(pathKey))
                {
                    string savedPath = PlayerPrefs.GetString(pathKey);
                    if (!string.IsNullOrEmpty(savedPath)) cfg.assignedFilePath = savedPath;
                }

                if (PlayerPrefs.HasKey(scaleKey))
                {
                    cfg.scaleMultiplier = PlayerPrefs.GetFloat(scaleKey, 1.0f);
                }
            }
        }
    }
}
