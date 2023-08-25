using MVR.FileManagementSecure;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using static OVRLipSync;

namespace JustAnotherUser {
    /**
     * © Mrmr32; please include `mrmr32.UVSwapper.<version>:/Custom/Scripts/mrmr32/UVSwapper/src/UVData.cs` in your .cslist
     **/
    public class UVData {
        public static int PLUGIN_VERSION = 5;
        public static string packagePath = "mrmr32.UVSwapper." + PLUGIN_VERSION + ":/";

        private static object _loadedLock = new object();
        private static bool _loaded = false,
                            _loading = false;
        
        public static readonly int FACE_REGION = 0,
                                    TORSO_REGION = 1,
                                    LIMBS_REGION = 2;

        // The first 506 UVs are in the margin of the mesh
        private static ushort _NUM_MARGIN_FACE_UVS;
        public static Vector2[] maleUV_face = null;
        public static Vector2[] femaleUV_face = null;
        public static ushort[,] mesh_face = null;

        // The first 1010 UVs are in the margin of the mesh
        private static ushort _NUM_MARGIN_TORSO_UVS;
        public static Vector2[] maleUV_torso = null;
        public static Vector2[] femaleUV_torso = null;
        public static ushort[,] mesh_torso = null;

        // The first 2198 UVs are in the margin of the mesh
        private static ushort _NUM_MARGIN_LIMBS_UVS;
        public static Vector2[] maleUV_limbs = null;
        public static Vector2[] femaleUV_limbs = null;
        public static ushort[,] mesh_limbs = null;

        /**
         * Loads the needed data to work.
         * The data will be ready when 
         **/
        public static void Load() {
            bool alreadyLoading = false;
            lock (UVData._loadedLock) {
                alreadyLoading = UVData._loading || UVData._loaded;
                if (!alreadyLoading) UVData._loading = true; // now I'm the one loading
            }
            if (!alreadyLoading) SuperController.LogMessage("Loading DAZ3D mesh data...");
            else {
                SuperController.LogMessage("Another process is already loading the DAZ3D mesh data");
                return;
            }

            new Thread(() => {
                try {
                    string text = FileManagerSecure.ReadAllText(UVData.packagePath + "Custom/Scripts/mrmr32/UVSwapper/src/UVData.json");
                    JSONNode jsonNode = JSON.Parse(text);

                    UVData._NUM_MARGIN_FACE_UVS = (ushort)jsonNode["face"]["MarginVerticesNum"].AsInt;
                    UVData.maleUV_face = UVData.loadUVs(jsonNode["face"]["MaleUVs"].AsArray);
                    UVData.femaleUV_face = UVData.loadUVs(jsonNode["face"]["FemaleUVs"].AsArray);
                    UVData.mesh_face = UVData.loadMesh(jsonNode["face"]["Mesh"].AsArray);

                    UVData._NUM_MARGIN_TORSO_UVS = (ushort)jsonNode["torso"]["MarginVerticesNum"].AsInt;
                    UVData.maleUV_torso = UVData.loadUVs(jsonNode["torso"]["MaleUVs"].AsArray);
                    UVData.femaleUV_torso = UVData.loadUVs(jsonNode["torso"]["FemaleUVs"].AsArray);
                    UVData.mesh_torso = UVData.loadMesh(jsonNode["torso"]["Mesh"].AsArray);

                    UVData._NUM_MARGIN_LIMBS_UVS = (ushort)jsonNode["limbs"]["MarginVerticesNum"].AsInt;
                    UVData.maleUV_limbs = UVData.loadUVs(jsonNode["limbs"]["MaleUVs"].AsArray);
                    UVData.femaleUV_limbs = UVData.loadUVs(jsonNode["limbs"]["FemaleUVs"].AsArray);
                    UVData.mesh_limbs = UVData.loadMesh(jsonNode["limbs"]["Mesh"].AsArray);

                    SuperController.LogMessage("DAZ3D mesh data loaded");
                } catch (Exception ex) {
                    SuperController.LogError(ex.ToString());
                } finally {
                    // all done
                    lock (UVData._loadedLock) {
                        UVData._loaded = true;
                        UVData._loading = false;
                    }
                }
            }).Start();
        }

        private static Vector2[] loadUVs(JSONArray uvNodes) {
            Vector2[] r = new Vector2[uvNodes.Count];
            for (int n = 0; n < uvNodes.Count; n++) {
                JSONNode node = uvNodes[n];
                r[n] = new Vector2(node["x"].AsFloat, node["y"].AsFloat);
            }
            return r;
        }

        private static ushort[,] loadMesh(JSONArray meshNodes) {
            ushort[,] r = new ushort[meshNodes.Count, 3];
            for (int n = 0; n < meshNodes.Count; n++) {
                JSONNode node = meshNodes[n];
                r[n, 0] = (ushort)node["d1"].AsInt;
                r[n, 1] = (ushort)node["d2"].AsInt;
                r[n, 2] = (ushort)node["d3"].AsInt;
            }
            return r;
        }

        public static bool IsLoaded() {
            bool value;
            lock (UVData._loadedLock) {
                value = UVData._loaded;
            }
            return value;
        }

        /**
         * Given a UV texture and the information about it, generate the texture for the opposite gender
         * @param original       Base texture
         * @param isOriginalMale true if the original texture is from a male, false if it's from a female
         * @param region         FACE_REGION, TORSO_REGION or LIMBS_REGION
         * @return New texture with the changed UVs
         **/
        public static Texture2D DeformUVs(Texture2D original, bool isOriginalMale, int region) {
            if (!UVData.IsLoaded()) throw new Exception("The mesh data is not loaded yet. Call `UVData.Load()` and then wait `UVData.IsLoaded()` to become true.");

            ushort marginVerticesSize = GetMarginVertices(region);
            int maxMarginVertice = (marginVerticesSize == 0) ? 0 : (marginVerticesSize - 1); // if no margin vertices, provide just one (it won't match with anyone else)

            return DeformTexture(original,
                                GetMesh(region),
                                GetUVs(isOriginalMale, region),
                                GetUVs(!isOriginalMale, region),
                                (ushort)maxMarginVertice, outerPointsPixelExpansion: 30);
        }

        /**
         * Given a texture, the mesh on how basePoints are connected, and the new basePoints positions (desiredPoints),
         * generate a new texture.
         * @ref (most of the code) made with ChatGPT3
         * @param texture                   Base texture
         * @param vertices                  Mesh
         * @param basePoints                x-y coordinates of the mesh
         * @param desiredPoints             Desired final x-y coordinates
         * @param maxIndexInOuterPoints     Max value of the vertice find in the outer region
         * @param outerPointsPixelExpansion For each set of consecutive points, expand the target texture N pixels
         * @param margin                    Number of additional pixels taken to correct floating-point error
         * @return New texture
         **/
        public static Texture2D DeformTexture(Texture2D texture, ushort[,] vertices, Vector2[] basePoints, Vector2[] desiredPoints, ushort maxIndexInOuterPoints, int outerPointsPixelExpansion = 0, float margin = 0.0f) {
            Assert.AreEqual(basePoints.Length, desiredPoints.Length);

            Texture2D result = new Texture2D(texture.width, texture.height);

            // the UVs goes from 0 to 1; scale the values between 0 and texture.width/height
            Vector2 scale = new Vector2(texture.width, texture.height);

            if (outerPointsPixelExpansion > 0) {
                // to solve the black bars problem we have to expand the pixels of the external region
                foreach (ushort[] consecutive in GetConsecutiveOuterPoints(vertices, maxIndexInOuterPoints)) {
                    var originalLine = new Vector2[] { basePoints[consecutive[0]] * scale, basePoints[consecutive[1]] * scale };
                    var desiredLine = new Vector2[] { desiredPoints[consecutive[0]] * scale, desiredPoints[consecutive[1]] * scale };

                    Color meanColor = GetMeanColor(texture, originalLine[0], originalLine[1]); // TODO instead of mean, the most predominant color
                    if (meanColor.a > 0) {
                        // in some sections of this code we assume the following: as we've scaled the vectors, a unit normal will be one pixel long

                        Vector2 vector = desiredLine[1] - desiredLine[0];
                        Vector2 normal = new Vector2(-vector.y, vector.x).normalized;

                        // we want to expand the texture by <outerPointsPixelExpansion> pixels following the normal, and as we're using a 45º angle
                        // at the end we want a vector of lenght sqrt(2*a^2)
                        float sqrt2 = 1.41f;

                        // we want to expand the filling as we get far from the vertice to compensate sharp angles
                        Vector2 A = desiredLine[0],
                                B = desiredLine[1],
                                C = desiredLine[0] + RotateVector(normal,45) * outerPointsPixelExpansion * sqrt2,
                                D = desiredLine[1] + RotateVector(normal, -45) * outerPointsPixelExpansion * sqrt2,
                                E = desiredLine[0] + RotateVector(-normal, -45) * outerPointsPixelExpansion * sqrt2,
                                F = desiredLine[1] + RotateVector(-normal, 45) * outerPointsPixelExpansion * sqrt2;
                        
                        /*******************************************************
                         * We have 4 triangles: ABC, BCD, ABE, BEF.
                         * 
                         *                     D
                         *             -       |
                         *       C              B
                         *          \      -       \
                         *            A               F
                         *             \        -
                         *              E
                         * 
                         * A,B are the base points.
                         * We'll fill all the polygon area with the same color.
                         *******************************************************/
                        PaintTriangle(result, new Vector2[] { A, B, C }, meanColor);
                        PaintTriangle(result, new Vector2[] { B, C, D }, meanColor);
                        PaintTriangle(result, new Vector2[] { A, B, E }, meanColor);
                        PaintTriangle(result, new Vector2[] { B, E, F }, meanColor);
                    }
                }
            }

            // now, we modify the old image to the new one
            int numVertices = vertices.Length / 3; // in C# int[a,b]#Length will return 'a*b'; we have N vertices, and 3 points for each one
            for (int i = 0; i < numVertices; i++) {
                var originalTriangle = new Vector2[] { basePoints[vertices[i,0]] * scale, basePoints[vertices[i,1]] * scale, basePoints[vertices[i,2]] * scale };
                var desiredTriangle = new Vector2[] { desiredPoints[vertices[i,0]] * scale, desiredPoints[vertices[i,1]] * scale, desiredPoints[vertices[i,2]] * scale };

                // Find the affine transformation matrix from the original triangle to the desired triangle
                var a = new Matrix4x4();
                for (int j = 0; j < 3; j++) {
                    a[0, j] = originalTriangle[j].x;
                    a[1, j] = originalTriangle[j].y;
                    a[2, j] = 1;
                }
                var b = new Matrix4x4();
                for (int j = 0; j < 3; j++) {
                    b[0, j] = desiredTriangle[j].x;
                    b[1, j] = desiredTriangle[j].y;
                    b[2, j] = 1;
                }
                a[3, 3] = b[3, 3] = 1; // @ref https://stackoverflow.com/a/57923547/9178470
                var inverseTransformMatrix = a * b.inverse; // instead of mapping the old values into the new one, we'll calculate where does the new pixels falls into the original image

                // In order to avoid iterating all the texture performing transformations, just iterate the part that will change
                Rect textureRect = GetTriangleBoundingRect(desiredTriangle, texture.width, texture.height, (margin == 0) ? 0 : (int)(margin + 1));
                // Apply the affine transformation to every pixel in the texture
                for (int x = (int)textureRect.xMin; x <= (int)textureRect.xMax; x++) {
                    for (int y = (int)textureRect.yMin; y <= (int)textureRect.yMax; y++) {
                        // don't deform something outside the base triangle
                        if (!IsPointInTriangle(x, y, desiredTriangle[0], desiredTriangle[1], desiredTriangle[2], margin)) continue;

                        //SuperController.LogMessage(x.ToString() + " - " + y.ToString());
                        var originalPosition = new Vector4(x, y, 1, 1);
                        var transformedPosition = inverseTransformMatrix * originalPosition;
                        var transformedX = (int)(transformedPosition.x / transformedPosition.w);
                        var transformedY = (int)(transformedPosition.y / transformedPosition.w);

                        // Skip the pixel if it's outside of the texture
                        if (transformedX < 0 || transformedX >= texture.width ||
                            transformedY < 0 || transformedY >= texture.height) {
                            continue;
                        }
                        
                        // Get the color from the transformed position and set it for the current pixel
                        var color = texture.GetPixel(transformedX, transformedY);
                        result.SetPixel(x, y, color);
                    }
                }
            }

            result.Apply();
            return result;
        }

        /**
         * Paint a triangle with one color
         * @ref made with ChatGPT3
         * @post Run `texture.Apply()`
         * @param texture Where to paint the triangle
         * @param triangle 3 vertices
         * @param color Color to paint
         **/
        private static void PaintTriangle(Texture2D texture, Vector2[] triangle, Color color) {
            Rect boundingRect = GetTriangleBoundingRect(triangle, texture.width, texture.height);

            for (int x = (int)boundingRect.xMin; x < (int)boundingRect.xMax; x++) {
                for (int y = (int)boundingRect.yMin; y < (int)boundingRect.yMax; y++) {
                    if (IsPointInTriangle(x, y, triangle[0], triangle[1], triangle[2])) {
                        texture.SetPixel(x, y, color);
                    }
                }
            }
        }

        /**
         * Rotate clock-wise a vector
         * @ref made with ChatGPT3
         * @param vector Vector to rotate
         * @param alpha Degrees to rotate the vector
         * @return Rotated vector
         **/
        private static Vector2 RotateVector(Vector2 vector, float alpha) {
            float rad = alpha * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(cos * vector.x - sin * vector.y, sin * vector.x + cos * vector.y);
        }

        /**
         * Merge two textures into the (uncloned) first
         * @post Run `texture.Apply()`
         **/
        public static Texture2D MergeTextures(Texture2D baseTexture, Texture2D toAdd) {
            toAdd = ResizeTexture(toAdd, baseTexture.width, baseTexture.height);

            for (int y = 0; y < baseTexture.height; y++) {
                for (int x = 0; x < baseTexture.width; x++) {
                    Color add = toAdd.GetPixel(x, y);
                    if (add.a == 0) continue; // transparent

                    if (add.a == 1) baseTexture.SetPixel(x, y, add);
                    else {
                        // semi-transparent
                        Color result = (1 - add.a) * baseTexture.GetPixel(x, y) + add.a * add;
                        baseTexture.SetPixel(x, y, result);
                    }
                }
            }

            return baseTexture;
        }

        /**
         * Clones a texture
         * @post Run `texture.Apply()`
         **/
        public static Texture2D CloneTexture(Texture2D originalTexture) {
            Texture2D copyTexture = new Texture2D(originalTexture.width, originalTexture.height);
            copyTexture.SetPixels(originalTexture.GetPixels());
            return copyTexture;
        }

        /**
         * Offsets the color of the (unclonned) texture
         * @post Run `texture.Apply()`
         * @param t Texture to offset
         * @param vector (HSL) offsets
         **/
        public static Texture2D OffsetTexture(Texture2D t, Vector3 vector) {
            SuperController.LogMessage("Offset: " + vector.x + " ; " + vector.y + " ; " + vector.z);
            for (int y = 0; y < t.height; y++) {
                for (int x = 0; x < t.width; x++) {
                    // current color
                    Color pixel = t.GetPixel(x, y);
                    if (pixel.a == 0) continue;
                    float hue, saturation, value;
                    Color.RGBToHSV(pixel, out hue, out saturation, out value);

                    float resultHue = hue + vector[0];
                    if (resultHue < 0) resultHue += 1.0f;
                    else if (resultHue > 1) resultHue -= 1.0f;

                    // set new color
                    Color result = Color.HSVToRGB(resultHue, Mathf.Clamp01(saturation + vector[1]), Mathf.Clamp01(value + vector[2]), false);
                    result.a = pixel.a;
                    t.SetPixel(x, y, result);
                }
            }

            return t;
        }

        /**
         * Resizes a texture (if needed)
         * @ref https://stackoverflow.com/a/56949497/9178470
         **/
        private static Texture2D ResizeTexture(Texture2D texture2D, int targetX, int targetY) {
            if (texture2D.width == targetX && texture2D.height == targetY) return texture2D;

            RenderTexture rt = new RenderTexture(targetX, targetY, 24);
            RenderTexture.active = rt;
            Graphics.Blit(texture2D, rt);
            Texture2D result = new Texture2D(targetX, targetY);
            result.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
            result.Apply();
            return result;
        }

        /**
         * Gets the mean color of a line
         * @ref made with ChatGPT3
         * @param texture Where to take the color
         * @param point1 Where the line starts
         * @param point2 Where the line ends
         * @return Mean color
         **/
        public static Color GetMeanColor(Texture2D texture, Vector2 point1, Vector2 point2, int numberOfSamples = 10) {
            Color[] samples = new Color[numberOfSamples];
            float step = 1.0f / (numberOfSamples - 1);
            float t = 0;
            for (int i = 0; i < numberOfSamples; i++, t += step) {
                Vector2 samplePoint = Vector2.Lerp(point1, point2, t);
                samples[i] = texture.GetPixel((int)samplePoint.x, (int)samplePoint.y); // TODO consider outside of texture
            }

            float r, g, b, a;
            r = g = b = a = 0f;
            foreach (Color sample in samples) {
                r += sample.r;
                g += sample.g;
                b += sample.b;
                a += sample.a;
            }
            return new Color(r / numberOfSamples, g / numberOfSamples, b / numberOfSamples, a / numberOfSamples);
        }

        /**
         * Gets the consecutive (joined by one triangle of the mesh) vertices that are in the outer region (from 0 to maxIndexInOuterPoints)
         * @ref made with ChatGPT3
         * @param mesh Group of triangles (3 sets of vertices) creating the mesh
         * @param maxIndexInOuterPoints Max value of the vertice find in the outer region
         **/
        private static List<ushort[]> GetConsecutiveOuterPoints(ushort[,] mesh, ushort maxIndexInOuterPoints) {
            List<ushort[]> result = new List<ushort[]>();

            for (int i = 0; i < mesh.Length / 3; i++) {
                for (int j = 0; j < 3; j++) {
                    ushort currentPoint = mesh[i,j];
                    ushort nextPoint = mesh[i,(j + 1) % 3];

                    if (currentPoint <= maxIndexInOuterPoints && nextPoint <= maxIndexInOuterPoints) {
                        result.Add(new ushort[] { currentPoint, nextPoint });
                    }
                }
            }

            return result;
        }

        /**
         * Given a point (x,y) and the vertices of the triangle (v1,v2,v3) get if the point is inside the triangle
         * @ref made with ChatGPT3
         * @param x First coordinate of the point
         * @param y Second coordinate of the point
         * @param v1 X-y coordinates of the first vertice of the triangle
         * @param v2 X-y coordinates of the second vertice of the triangle
         * @param v3 X-y coordinates of the third vertice of the triangle
         * @param margin Try to compensate floating-point error
         * @return If the point is inside the triangle
         **/
        private static bool IsPointInTriangle(float x, float y, Vector2 v1, Vector2 v2, Vector2 v3, float margin = 0f) {
            float denominator = ((v2.y - v3.y) * (v1.x - v3.x) + (v3.x - v2.x) * (v1.y - v3.y));
            float a = ((v2.y - v3.y) * (x - v3.x) + (v3.x - v2.x) * (y - v3.y)) / denominator;
            float b = ((v3.y - v1.y) * (x - v3.x) + (v1.x - v3.x) * (y - v3.y)) / denominator;
            float c = 1 - a - b;
            if (0 <= a && a <= 1 && 0 <= b && b <= 1 && 0 <= c && c <= 1) return true;

            // at this point it's pretty sure it's outside the triangle
            if (margin == 0f) return false;

            float dist1Sqr = Mathf.Pow(x - v1.x, 2) + Mathf.Pow(y - v1.y, 2);
            float dist2Sqr = Mathf.Pow(x - v2.x, 2) + Mathf.Pow(y - v2.y, 2);
            float dist3Sqr = Mathf.Pow(x - v3.x, 2) + Mathf.Pow(y - v3.y, 2);
            float line1Sqr = Mathf.Pow(v2.x - v1.x, 2) + Mathf.Pow(v2.y - v1.y, 2);
            float line2Sqr = Mathf.Pow(v3.x - v2.x, 2) + Mathf.Pow(v3.y - v2.y, 2);
            float line3Sqr = Mathf.Pow(v1.x - v3.x, 2) + Mathf.Pow(v1.y - v3.y, 2);

            float distToLine1Sqr = Mathf.Pow((x - v1.x) * (v2.y - v1.y) - (y - v1.y) * (v2.x - v1.x), 2) / line1Sqr;
            float distToLine2Sqr = Mathf.Pow((x - v2.x) * (v3.y - v2.y) - (y - v2.y) * (v3.x - v2.x), 2) / line2Sqr;
            float distToLine3Sqr = Mathf.Pow((x - v3.x) * (v1.y - v3.y) - (y - v3.y) * (v1.x - v3.x), 2) / line3Sqr;

            return a >= -margin && a <= 1 + margin && b >= -margin && b <= 1 + margin && c >= -margin && c <= 1 + margin;
        }

        /**
         * Given a triangle (3 vertices) and the texture size, get the rectangle fitting only the triangle
         * @ref made with ChatGPT3
         * @param triangle The 3 vertices, with its x-y coordinates
         * @param textureWidth Texture width
         * @param textureHeight Texture height
         * @param margin Try to compensate floating-point error
         * @return A rectangle fitting the triangle
         **/
        private static Rect GetTriangleBoundingRect(Vector2[] triangle, int textureWidth, int textureHeight, int margin = 0) {
            float xMin = Mathf.Min(triangle[0].x, triangle[1].x, triangle[2].x) - margin;
            float xMax = Mathf.Max(triangle[0].x, triangle[1].x, triangle[2].x) + margin;
            float yMin = Mathf.Min(triangle[0].y, triangle[1].y, triangle[2].y) - margin;
            float yMax = Mathf.Max(triangle[0].y, triangle[1].y, triangle[2].y) + margin;

            xMin = Mathf.Clamp(xMin, 0, textureWidth - 1);
            xMax = Mathf.Clamp(xMax, 0, textureWidth - 1);
            yMin = Mathf.Clamp(yMin, 0, textureHeight - 1);
            yMax = Mathf.Clamp(yMax, 0, textureHeight - 1);

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /**
         * Given the gender (male/female) and region (face/torso/limbs), it returns the x-y coordinates
         * of the UVs.
         * @param isMale true if it is a male, false if it is a female
         * @param region FACE_REGION, TORSO_REGION or LIMBS_REGION
         * @return All the UVs given the arguments
         **/
        public static Vector2[] GetUVs(bool isMale, int region) {
            if (isMale) {
                if (region == FACE_REGION) return maleUV_face;
                else if (region == TORSO_REGION) return maleUV_torso;
                else if (region == LIMBS_REGION) return maleUV_limbs;
            }
            else {
                if (region == FACE_REGION) return femaleUV_face;
                else if (region == TORSO_REGION) return femaleUV_torso;
                else if (region == LIMBS_REGION) return femaleUV_limbs;
            }
            return null;
        }

        /**
         * Given the region (face/torso/limbs), it returns the mesh that joins all the UVs.
         * The mesh is equal for both male and female.
         * Note: This mesh may differ on the original DAZ3D G2 UV mesh.
         * @param region FACE_REGION, TORSO_REGION or LIMBS_REGION
         * @return All the vertices given the arguments; each vertice specifies one point of the UV
         **/
        public static ushort[,] GetMesh(int region) {
            if (region == FACE_REGION) return mesh_face;
            else if (region == TORSO_REGION) return mesh_torso;
            else if (region == LIMBS_REGION) return mesh_limbs;
            return null;
        }

        public static ushort GetMarginVertices(int region) {
            if (region == FACE_REGION) return _NUM_MARGIN_FACE_UVS;
            else if (region == TORSO_REGION) return _NUM_MARGIN_TORSO_UVS;
            else if (region == LIMBS_REGION) return _NUM_MARGIN_LIMBS_UVS;
            return 0;
        }
    }
}