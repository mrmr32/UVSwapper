using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimpleJSON; // JSONNode

namespace JustAnotherUser {
    /**
     * @ref CUAManager.17 (plugin by Blazedust)
     **/
    public class CUAManagerPath {
        public string PluginPath { get { return cachedPluginPath; } }
        public string PluginPresetPath { get { return cachedPluginPresetPath; } }
        public string PluginDataFolder { get { return this._pluginDataFolder; } }

        private string cachedPluginPresetPath = "";
        private string cachedPluginPath = "";

        private MVRScript _script;
        private string _pluginDataFolder;

        public CUAManagerPath(MVRScript script, string pluginDataFolder = "Saves\\PluginData\\mrmr32\\UVSwapper") {
            this._script = script;
            this._pluginDataFolder = pluginDataFolder;
        }

        public void Init() {
            SetPluginPath(this._script);
            SetPluginPresetPath(this._script);
        }

        public static string Combine(string a, string b) {
            return a + (a.EndsWith("/") ? "" : "/") + b;
        }

        public bool VerifyPluginDataFolder() {
            string[] existingFiles;
            try {
                existingFiles = SuperController.singleton.GetFilesAtPath(this._pluginDataFolder/*, "*.json"*/);
            }
            catch (Exception e) {
                //SuperController.singleton.ClearErrors();
                if (e.GetType().FullName == "System.IO.DirectoryNotFoundException" || e.Message != null && e.Message.Contains("non-existent path"))
                {
                    SuperController.LogMessage("Missing folder '" + this._pluginDataFolder + "'. Attempting to automatically create it.");
                    if (CUAManagerPath.TryToCreatePluginFolder(this._pluginDataFolder)) return true;
                }
                SuperController.LogError("Looks like the folder '" + this._pluginDataFolder + "' is missing. Plugin can't start without it. Create the folder and reload the plugin.");
                return false;
            }
            return true;
        }
        
        public static Atom GetFirstAtomOfType(string[] prioAtomTypes) {
            Atom[] prioAtoms = new Atom[prioAtomTypes.Length + 1];
            foreach (Atom a in SuperController.singleton.GetAtoms()) {
                bool isMatchingType = false;
                for (int i = 0; i < prioAtomTypes.Length; i++)
                {
                    isMatchingType = (a.type == prioAtomTypes[i]);
                    if (isMatchingType && prioAtoms[i] == null)
                    {
                        prioAtoms[i] = a;
                    }
                }
                if (!isMatchingType && prioAtoms[prioAtoms.Length - 1] == null)
                {
                    prioAtoms[prioAtoms.Length - 1] = a;
                }
            }
            return prioAtoms.Where((x) => { return x != null; }).FirstOrDefault();
        }

        public static bool TryToCreatePluginFolder(string presetPath) {
            // Attempt to create the folder. Do this by saving a dummy-file
            Camera cam = SuperController.singleton.screenshotCamera;
            try {
                string nPath = presetPath + "/dummy.json";
                // For the dummy-save to automatically create the directory, the path requires backslashes! A little quirk.
                nPath = nPath.Replace("/", "\\");

                // disable screenshot taking 
                // don't do this if you don't know what you're doing! 
                // This might not be allowed in future versions of VaM
                // we do restore it in the end of this function though!
                SuperController.singleton.screenshotCamera = null;

                // Save a dummy file only containing one atom to keep it small and speedy.
                Atom prioAtom = CUAManagerPath.GetFirstAtomOfType(new string[3] { "WindowCamera", "PlayerNavigationPanel", "InvisibleLight" });

                SuperController.singleton.Save(nPath, prioAtom, includePhysical: false, includeAppearance: false);

                // check files again!
                try {
                    string[] existingFiles = SuperController.singleton.GetFilesAtPath(presetPath, "*.json");

                    // Can read files now, assuming folder was created!
                    SuperController.LogMessage("Creating folder successful!");

                    // plugin folder created!
                    return true;
                }
                catch (Exception) { }
            }
            catch (Exception ex) {
                // Even failed to create the directory with a dummy file... User must create this folder!
                SuperController.LogMessage("Automatic creation failed");
                SuperController.LogMessage(ex.ToString());
            }
            finally {
                SuperController.singleton.screenshotCamera = cam;
            }

            return false;
        }

        private string SetPluginPresetPath(MVRScript script) {
            if (cachedPluginPresetPath != "") return cachedPluginPresetPath;
            SetPluginPath(script);
            cachedPluginPresetPath = CUAManagerPath.Combine(cachedPluginPath, "presets");
            return cachedPluginPresetPath;
        }

        private string SetPluginPath(MVRScript script) {
            if (cachedPluginPath != "") return cachedPluginPath;

            string loadDir = SuperController.singleton.currentLoadDir;
            // Log(SuperController.singleton.currentSaveDir);
            // Log(script.storeId);
            string pluginId = script.storeId.Split('_')[0]; // first part is the plugin ID in the MVRPluginManager.
                                                            // MVRPluginManager manager = containingAtom.GetStorableByID("PluginManager") as MVRPluginManager; // this is needed to find the manager on non-session plugins.
            MVRPluginManager man = (script.manager != null ? script.manager : (script.containingAtom.GetStorableByID("PluginManager") as MVRPluginManager)); // Not tested on a non-session plugin, but should work!
            string pathToScriptFile = man.GetJSON(true, true)["plugins"][pluginId].Value;
            // SuperController.LogError(pathToScriptFile); // "./Scripts/FolderA/PluginFolder/ADD_ME.cslist"
            string pathToScriptFolder = "";
            if (pathToScriptFile.Contains("/") || pathToScriptFile.Contains("\\")) { pathToScriptFolder = pathToScriptFile.Substring(0, pathToScriptFile.LastIndexOfAny(new char[] { '/', '\\' })); }
            if (pathToScriptFolder.StartsWith(".")) { pathToScriptFolder = pathToScriptFolder.Substring(1); }
            if (pathToScriptFolder.StartsWith("/")) { pathToScriptFolder = pathToScriptFolder.Substring(1); }
            // SuperController.LogError(pathToScriptFolder); // "Scripts/FolderA/PluginFolder" OR "AddonPackageVarFile.#:/Scripts/FolderA/PluginFolder" for var-packages.
            // Plugin path should be a non-varpackage container (don't write back to var-packages, let preset files be in the folder structure, and for backwards compatibility for older versions of this plugin!)
            // Failsafe: We only cut the first "/" if it is preceded by a ":" character, assuming var packages are always split with the ":/" separator.
            int varPackageIndex = pathToScriptFolder.IndexOf("/");
            if (varPackageIndex > 0 && pathToScriptFolder.IndexOf(":/") == (varPackageIndex - 1)) {
                pathToScriptFolder = pathToScriptFolder.Substring(varPackageIndex + 1);
            }
            if (!pathToScriptFolder.StartsWith("Custom/Scripts/")) {
                // Assuming the script is in the same folder as the scene file OR in a subfolder of the scene file when the path doesn't start with "Custom/Scripts/".
                if (pathToScriptFolder != "") {
                    if (loadDir != "") pathToScriptFolder = loadDir + "/" + pathToScriptFolder;
                }
                else {
                    pathToScriptFolder = loadDir;
                }
            }
            // SuperController.LogError(pathToScriptFolder); // "Custom/Scripts/FolderA/PluginFolder" OR  "Saves/scene/User/SceneName/Scripts/FolderA/PluginFolder"
            cachedPluginPath = pathToScriptFolder;
            return cachedPluginPath;
        }
    }
}