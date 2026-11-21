using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ferrous.SpriteStack.Editor
{
    internal sealed class SpriteStackWindow : EditorWindow
    {
        [SerializeField] private bool useSheet;
        [SerializeField] private Texture2D sheet, top, side, front, back;
        [SerializeField] private bool fit, topNoseUp, sideNoseRight;
        [SerializeField] private bool mirrorBack = true;
        [SerializeField] private int alphaCutoff = 128;
        [SerializeField] private float pixelsPerUnit = 32f, heading, squash = .65f, spacing = 1f;
        [SerializeField] private string assetName = "FerrousSpriteStack";
        private SpriteStackBuilder.Stack stack;
        private Texture2D preview;
        private Vector2 scroll;
        private string status;
        private MessageType statusType;

        [MenuItem("Tools/Ferrous/Sprite Stack Builder")]
        private static void Open() => GetWindow<SpriteStackWindow>("Sprite Stack Builder");

        private void OnDisable() => ClearPreview();

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Orthographic pixel art", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            useSheet = EditorGUILayout.Toggle("Use 2×2 sheet", useSheet);
            if (useSheet)
            {
                sheet = TextureField("Sheet PNG", sheet);
                EditorGUILayout.HelpBox("Top row: Top / Side. Bottom row: Front / Back. Use four equal cells.", MessageType.Info);
            }
            else
            {
                top = TextureField("Top PNG", top);
                side = TextureField("Side PNG", side);
                front = TextureField("Front PNG", front);
                back = TextureField("Back PNG (optional)", back);
            }
            topNoseUp = EditorGUILayout.Toggle("Top faces up", topNoseUp);
            sideNoseRight = EditorGUILayout.Toggle("Side faces right", sideNoseRight);
            mirrorBack = EditorGUILayout.Toggle("Mirror back view", mirrorBack);
            fit = EditorGUILayout.Toggle("Fit mismatched views", fit);
            alphaCutoff = EditorGUILayout.IntSlider("Alpha cutoff", alphaCutoff, 1, 255);
            if (EditorGUI.EndChangeCheck())
            {
                stack = null;
                status = null;
                ClearPreview();
            }
            if (GUILayout.Button("Build slice preview")) BuildPreview();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prefab presentation", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            heading = EditorGUILayout.Slider("Starting heading", heading, 0f, 360f);
            squash = EditorGUILayout.Slider("Vertical squash", squash, .15f, 1f);
            spacing = EditorGUILayout.Slider("Slice rise (pixels)", spacing, 0f, 4f);
            if (EditorGUI.EndChangeCheck() && stack != null) RefreshPreview();
            pixelsPerUnit = Mathf.Clamp(EditorGUILayout.FloatField("Pixels per unit", pixelsPerUnit), 1f, 1024f);
            assetName = EditorGUILayout.TextField("Prefab name", assetName);

            if (preview != null)
            {
                var rect = GUILayoutUtility.GetRect(1f, 260f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rect, new Color(.14f, .15f, .18f));
                float scale = Mathf.Min((rect.width - 12f) / preview.width, (rect.height - 12f) / preview.height);
                if (scale >= 1f) scale = Mathf.Floor(scale);
                if (scale > 0f)
                {
                    var size = new Vector2(preview.width, preview.height) * scale;
                    GUI.DrawTexture(new Rect(rect.center - size * .5f, size), preview);
                }
            }
            if (GUILayout.Button("Save stack prefab…")) SavePrefab();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);
            EditorGUILayout.EndScrollView();
        }

        private static Texture2D TextureField(string label, Texture2D value)
            => (Texture2D)EditorGUILayout.ObjectField(label, value, typeof(Texture2D), false);

        private void ClearPreview()
        {
            if (preview != null) DestroyImmediate(preview);
            preview = null;
        }

        private void RefreshPreview()
        {
            ClearPreview();
            preview = SpriteStackBuilder.Preview(stack, heading, squash, spacing);
            Repaint();
        }

        private void BuildPreview()
        {
            try
            {
                SpriteStackBuilder.View t, s, f, b;
                if (useSheet)
                {
                    var source = SpriteStackBuilder.ReadPng(sheet);
                    if (source.Width % 2 != 0 || source.Height % 2 != 0)
                        throw new InvalidOperationException("The sheet must divide evenly into four cells.");
                    int w = source.Width / 2, h = source.Height / 2;
                    t = source.Crop(0, h, w, h);
                    s = source.Crop(w, h, w, h);
                    f = source.Crop(0, 0, w, h);
                    b = source.Crop(w, 0, w, h);
                }
                else
                {
                    t = SpriteStackBuilder.ReadPng(top);
                    s = SpriteStackBuilder.ReadPng(side);
                    f = SpriteStackBuilder.ReadPng(front);
                    b = back != null ? SpriteStackBuilder.ReadPng(back) : null;
                }
                stack = SpriteStackBuilder.Build(t, s, f, b, alphaCutoff, fit, topNoseUp, sideNoseRight, mirrorBack);
                SpriteStackBuilder.AtlasColumns(stack);
                RefreshPreview();
                status = $"{stack.Width}×{stack.Depth} footprint · {stack.Slices.Count} visible slices";
                statusType = MessageType.Info;
            }
            catch (Exception e)
            {
                stack = null;
                ClearPreview();
                status = e.Message;
                statusType = MessageType.Error;
            }
        }

        private void SavePrefab()
        {
            BuildPreview();
            if (stack == null) return;
            string name = assetName?.Trim();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.IndexOfAny(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }) >= 0 || name.EndsWith(".", StringComparison.Ordinal))
            {
                status = "Enter a valid prefab name.";
                statusType = MessageType.Error;
                return;
            }

            string absolute = EditorUtility.OpenFolderPanel("Choose a folder under Assets", Application.dataPath, "");
            if (string.IsNullOrEmpty(absolute)) return;
            string assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            absolute = Path.GetFullPath(absolute).Replace('\\', '/').TrimEnd('/');
            var comparison = Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(absolute, assets, comparison) && !absolute.StartsWith(assets + "/", comparison))
            {
                status = "Choose a folder inside this Unity project's Assets directory.";
                statusType = MessageType.Error;
                return;
            }
            string parent = "Assets" + absolute.Substring(assets.Length);
            string folder = null;
            GameObject root = null;
            Texture2D atlasPixels = null;
            try
            {
                string unique = AssetDatabase.GenerateUniqueAssetPath(parent + "/" + name);
                string guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(unique));
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Could not create the output folder.");
                folder = AssetDatabase.GUIDToAssetPath(guid);
                int columns = SpriteStackBuilder.AtlasColumns(stack), count = stack.Slices.Count;
                int tileW = stack.Width + 2 * SpriteStackBuilder.Gutter;
                int tileH = stack.Depth + 2 * SpriteStackBuilder.Gutter;
                int atlasWidth = columns * tileW, atlasHeight = (count + columns - 1) / columns * tileH;
                var pixels = new Color32[atlasWidth * atlasHeight];
                var regions = new Rect[count];
                for (int i = 0; i < count; i++)
                {
                    int x = i % columns * tileW + SpriteStackBuilder.Gutter;
                    int y = i / columns * tileH + SpriteStackBuilder.Gutter;
                    for (int row = 0; row < stack.Depth; row++)
                        Array.Copy(stack.Slices[i].Pixels, row * stack.Width, pixels, (y + row) * atlasWidth + x, stack.Width);
                    regions[i] = new Rect(x, y, stack.Width, stack.Depth);
                }

                atlasPixels = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
                atlasPixels.SetPixels32(pixels);
                atlasPixels.Apply(false, false);
                string pngPath = folder + "/" + name + "_Slices.png";
                File.WriteAllBytes(pngPath, atlasPixels.EncodeToPNG());
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Unity could not import the slice texture.");
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = SpriteStackBuilder.MaxAtlasSize;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                if (texture == null || texture.width != atlasWidth || texture.height != atlasHeight)
                    throw new InvalidOperationException("The imported atlas dimensions changed. Check the platform texture settings.");

                var atlas = CreateInstance<FerrousSpriteStackAtlas>();
                atlas.texture = texture;
                atlas.slices = new Sprite[count];
                string atlasPath = folder + "/" + name + "_Sprites.asset";
                AssetDatabase.CreateAsset(atlas, atlasPath);
                root = new GameObject(name, typeof(SortingGroup), typeof(FerrousSpriteStack));
                var pivots = new Transform[count];
                var heights = new int[count];
                for (int i = 0; i < count; i++)
                {
                    int z = stack.Slices[i].HeightIndex;
                    var sprite = Sprite.Create(texture, regions[i], new Vector2(.5f, .5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = $"Slice_{z:D3}";
                    AssetDatabase.AddObjectToAsset(sprite, atlasPath);
                    atlas.slices[i] = sprite;
                    var pivot = new GameObject($"Layer_{z:D3}").transform;
                    pivot.SetParent(root.transform, false);
                    var child = new GameObject($"Sprite_{z:D3}", typeof(SpriteRenderer));
                    child.transform.SetParent(pivot, false);
                    var renderer = child.GetComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingOrder = i;
                    pivots[i] = pivot;
                    heights[i] = z;
                }
                EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssets();
                root.GetComponent<FerrousSpriteStack>().Configure(pivots, heights, pixelsPerUnit, heading, squash, spacing);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab", out bool saved);
                if (!saved || prefab == null) throw new InvalidOperationException("Unity could not save the stack prefab.");
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                status = "Saved " + AssetDatabase.GetAssetPath(prefab);
                statusType = MessageType.Info;
            }
            catch (Exception e)
            {
                if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
                status = e.Message;
                statusType = MessageType.Error;
            }
            finally
            {
                if (root != null) DestroyImmediate(root);
                if (atlasPixels != null) DestroyImmediate(atlasPixels);
            }
        }
    }
}
