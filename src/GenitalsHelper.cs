using MVR.FileManagementSecure;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace JustAnotherUser {
    public class GenitalsHelper {
        /**
         * Cached `GetTemplates()` results
         **/
        private static Dictionary<string, Texture2D> maleTemplates = null,
                                                    femaleTemplates = null;

        private static readonly Vector3 MEAN_DICK_COLOR = new Vector3(21.0f / 360, 0.35f, 0.91f);

        public static IEnumerable<GenitalsTexture> LoadFemaleGenitals(Texture2D femaleTorso) {
            Dictionary<string, GenitalsTexture> r = new Dictionary<string, GenitalsTexture>();
            Dictionary<string, Texture2D> template = GetTemplates(false);
            foreach (string type in new string[] { "diffuse", "specular", "normal", "gloss" }) {
                if (type == "diffuse") {
                    UVData.MergeTextures(femaleTorso, template["diffuse"]);
                    femaleTorso.Apply();
                    r[type] = new GenitalsTexture(type, femaleTorso);
                }
                else {
                    // merge it with the default texture
                    // TODO
                }
            }
            return r.Values;
        }

        /**
         * Given the skin color (a torso) it will get the "difference of color" between that and the mean dick color
         **/
        private static Vector3 GetDickOffsetVector(Texture2D femaleTorso) {
            // we'll get the mean color of a line near the genitals region, on the torso
            Vector2 p1 = new Vector2(0.467f * femaleTorso.width, 0.736f * femaleTorso.height),
                p2 = new Vector2(0.427f * femaleTorso.width, 0.755f * femaleTorso.height);
            Color mean = UVData.GetMeanColor(femaleTorso, p1, p2, 200);
            float hue, saturation, value;
            Color.RGBToHSV(mean, out hue, out saturation, out value);
            Vector3 offset = new Vector3(hue, saturation, value) - MEAN_DICK_COLOR;
            return offset;
        }

        public static IEnumerable<GenitalsTexture> LoadMaleGenitals(Texture2D femaleTorso) {
            Dictionary<string, GenitalsTexture> r = new Dictionary<string, GenitalsTexture>();
            Dictionary<string, Texture2D> template = GetTemplates(true);
            foreach (string type in new string[] { "diffuse", "specular", "normal", "gloss" }) {
                if (type == "diffuse") {
                    Texture2D diffuse = UVData.MergeTextures(UVData.OffsetTexture(UVData.CloneTexture(template["diffuse"]), GetDickOffsetVector(femaleTorso)), template["diffuseOver"]);
                    diffuse.Apply();
                    r[type] = new GenitalsTexture(type, diffuse);
                }
                else {
                    // keep default
                    r[type] = new GenitalsTexture(type, template[type]);
                }
            }
            return r.Values;
        }

        private static Dictionary<string, Texture2D> GetTemplates(bool fromMale) {
            // result already cached?
            if (fromMale) {
                if (maleTemplates != null) return maleTemplates;
            }
            else {
                if (femaleTemplates != null) return maleTemplates;
            }
            // not cached; load
            if (UVData.packagePath == null) throw new NullReferenceException("Plugin version not loaded");

            Dictionary<string, Texture2D> r = new Dictionary<string, Texture2D>();
            string[] files = (fromMale ? new string[] { "diffuse", "diffuseOver", "specular", "normal", "gloss" } : new string[] { "diffuse", "specular", "normal", "gloss" });
            foreach (string file in files) {
                string fileName = "";
                if (file == "diffuse") fileName = "genitalsD.png";
                else if (file == "diffuseOver") fileName = "genitalsD_tip.png";
                else if (file == "specular") fileName = "genitalsS.png";
                else if (file == "normal") fileName = "genitalsN.png";
                else if (file == "gloss") fileName = "genitalsG.png";

                try {
                    Texture2D text = new Texture2D(1, 1); // it will be changed later by `ImageConversion.LoadImage`
                    byte[] originalData = FileManagerSecure.ReadAllBytes(UVData.packagePath + "Custom/Scripts/mrmr32/UVSwapper/genitals/" + (fromMale ? "male" : "female") + "/" + fileName);
                    ImageConversion.LoadImage(text, originalData);
                    r[file] = text;
                }
                catch (Exception ex) {
                    SuperController.LogError(ex.ToString());
                }
            }

            // cache the result
            if (fromMale) {
                maleTemplates = r;
            }
            else {
                femaleTemplates = r;
            }
            return r;
        }

        public class GenitalsTexture {
            public string typeOfTexture;
            public Texture2D texture;

            public GenitalsTexture(string type, Texture2D texture) {
                this.typeOfTexture = type;
                this.texture = texture;
            }
        }
    }
}
