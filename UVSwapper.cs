using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SimpleJSON; // JSONNode
using System.Threading;
using MVR.FileManagementSecure; // WriteAllBytes


namespace JustAnotherUser {
    public class UVSwapper : MVRScript {
        private static readonly string VERSION = "0.2";
        private static readonly int UUID_LENGTH = 8;

        public static readonly int DIFFUSE_TEXTURE = 0,
                                    SPECULAR_TEXTURE = 1,
                                    GLOSS_TEXTURE = 2,
                                    NORMAL_TEXTURE = 3,
                                    DECAL_TEXTURE = 5;

        //private DecalMakerHelper _helper;
        private CUAManagerPath _path;

        private DAZCharacterSelector _dazCharacterSelector;

        private string _saveName;

        public override void Init() {
            // plugin VaM GUI description
            pluginLabelJSON.val = "UVSwapper v" + VERSION;

            this._path = new CUAManagerPath(this);
            this._path.VerifyPluginDataFolder();
            this._path.Init();

            this._dazCharacterSelector = containingAtom.GetComponentInChildren<DAZCharacterSelector>();
            if (this._dazCharacterSelector == null) {
                SuperController.LogError("You need to aply UVSwapper on a Person atom");
                return;
            }

            // TODO add scene name
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            this._saveName = "";
            for (int i = 0; i < UUID_LENGTH; i++) _saveName += chars[(int)UnityEngine.Random.Range(0,chars.Length)];

            this.SetupUI();
        }

        public void SetTexture(int region, int typeOfTexture, string path) {
            // get the storable name
            string storableName;
            if (region == UVData.FACE_REGION) storableName = "face";
            else if (region == UVData.TORSO_REGION) storableName = "torso";
            else /*if (region == UVData.LIMBS_REGION)*/ storableName = "limbs";

            if (typeOfTexture == DIFFUSE_TEXTURE) storableName += "Diffuse";
            else if (typeOfTexture == SPECULAR_TEXTURE) storableName += "Specular";
            else if (typeOfTexture == NORMAL_TEXTURE) storableName += "Normal";
            else if (typeOfTexture == DECAL_TEXTURE) storableName += "Decal";
            else /*if (typeOfTexture == GLOSS_TEXTURE)*/ storableName += "Gloss";

            storableName += "Url";

            // save it
            DAZCharacterTextureControl textures = this.containingAtom.GetStorableByID("textures") as DAZCharacterTextureControl;
            JSONClass json = textures.GetJSON();
            json[storableName] = path;
            textures.RestoreFromJSON(json);
        }


        private void SetupUI() {
            string baseName = this._saveName + "_" + containingAtom.name;

            foreach (string part in new string[]{ "face", "torso", "limbs" }) {
                int region;
                if (part == "face") region = UVData.FACE_REGION;
                else if (part == "torso") region = UVData.TORSO_REGION;
                else region = UVData.LIMBS_REGION;

                JSONStorableString label = new JSONStorableString("label_" + part, part);
                UIDynamicTextField labelObject = CreateTextField(label);
                labelObject.height = 40f;

                foreach (string type in new string[] { "diffuse", "specular", "normal", "decal", "gloss" }) {
                    int typeNum;
                    if (type == "diffuse") typeNum = DIFFUSE_TEXTURE;
                    else if (type == "specular") typeNum = SPECULAR_TEXTURE;
                    else if (type == "normal") typeNum = NORMAL_TEXTURE;
                    else if (type == "decal") typeNum = DECAL_TEXTURE;
                    else typeNum = GLOSS_TEXTURE;

                    JSONStorableUrl urlJSON = new JSONStorableUrl(part + "DiffuseUrl", string.Empty, (val) => StartCoroutine(this.LoadTexture(baseName, val, region, typeNum)), "jpg|jpeg|png|tif|tiff", "Custom/Atom/Person/Textures", true);
                    urlJSON.suggestedPathGroup = "DAZCharacterTexture";

                    UIDynamicButton btn = CreateButton("set " + type);
                    urlJSON.fileBrowseButton = btn.button;
                }
            }
        }

        protected void Start() {
            UVData.Load();
        }

        protected void Update() {
            //this._helper.Update();
        }

        private bool IsMale() {
            DAZCharacter dazCharacter = this._dazCharacterSelector.selectedCharacter;
            return dazCharacter.isMale;
        }

        private IEnumerator LoadTexture(string baseOutFileName, string texturePath, int region, int typeOfTexture) {
            Texture2D originalTexture = new Texture2D(1, 1); // it will be changed later by `ImageConversion.LoadImage`
            bool isMale = true;

            try {
                SuperController.LogMessage("Converting image '" + texturePath + "'...");

                // get the atom sex
                isMale = IsMale();

                // get the new image
                byte[] originalData = FileManagerSecure.ReadAllBytes(texturePath);
                ImageConversion.LoadImage(originalTexture, originalData);
            } catch (Exception ex) {
                SuperController.LogError(ex.ToString());
            }

            // wait the UVData to finish loading
            yield return new WaitUntil(() => UVData.IsLoaded());

            try {
                // distort the image
                Texture2D targetTexture = UVData.DeformUVs(originalTexture, !isMale /* the original texture is the opposite as the current gender */, region);

                // get the output path
                // @ref CUAManager
                string folder = this._path.PluginDataFolder;
                string filePath = CUAManagerPath.Combine(folder, baseOutFileName + "-" + region.ToString() + "_" + typeOfTexture.ToString() + ".png");
                filePath = filePath.Replace("\\", "/"); // Sanitize input, just-in-case.
                
                // convert the output image back into bytes
                byte[] image = ImageConversion.EncodeToPNG(targetTexture);
                
                // write the output image
                FileManagerSecure.WriteAllBytes(filePath, image);
                
                if (FileManagerSecure.FileExists(filePath)) {
                    SuperController.LogMessage("Image '" + texturePath + "' converted as '" + filePath + "'.");
                    SetTexture(region, typeOfTexture, filePath);
                }
                else {
                    SuperController.LogError("Error while converting image '" + texturePath + "'");
                }
            } catch (Exception ex) {
                SuperController.LogError(ex.ToString());
            }
        }
    }
}