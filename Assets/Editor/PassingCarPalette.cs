using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The colour pool the passing-car painter randomises from.
///
/// The colours live here rather than as assets somebody has to make by hand, and the materials are written
/// into Assets/Prefabs/Cars/Colors the first time they are asked for, so the palette is one list to edit and
/// the project still ends up with ordinary, editable materials - the painter's cars share one material per
/// colour instead of carrying a copy each, and a colour can be tweaked in the Inspector without touching this
/// file. A material that already exists is left alone: recolouring one in the project sticks.
///
/// Each colour carries its own finish, which is what stops the fleet looking like plastic toys: the metallics
/// (silver, gold, copper) are shiny and reflective, the flat colours are matte.
/// </summary>
public static class PassingCarPalette
{
    /// <summary>Where the generated materials are kept, under the folder the passing cars come from.</summary>
    public const string Folder = "Assets/Prefabs/Cars/Colors";

    /// <summary>
    /// Copied for its shader and its keywords, so a generated material is the same kind of material the
    /// hand-made CarColor assets are.
    /// </summary>
    private const string TemplatePath = "Assets/Prefabs/Cars/CarColor.mat";

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    private struct Coat
    {
        public string name;
        public Color color;
        public float metallic;
        public float smoothness;

        public Coat(string name, float r, float g, float b, float metallic, float smoothness)
        {
            this.name = name;
            this.color = new Color(r, g, b, 1f);
            this.metallic = metallic;
            this.smoothness = smoothness;
        }
    }

    /// <summary>
    /// The palette. Add a line to add a colour: the material appears in the project and the painter starts
    /// using it the next time either runs.
    /// </summary>
    private static readonly Coat[] Coats =
    {
        new Coat("White",         0.93f, 0.93f, 0.92f, 0.10f, 0.65f),
        new Coat("Silver",        0.78f, 0.79f, 0.82f, 0.85f, 0.85f),
        new Coat("Grey",          0.47f, 0.48f, 0.50f, 0.30f, 0.60f),
        new Coat("Graphite",      0.16f, 0.17f, 0.19f, 0.45f, 0.70f),
        new Coat("Black",         0.05f, 0.05f, 0.06f, 0.30f, 0.75f),
        new Coat("Navy",          0.07f, 0.12f, 0.32f, 0.35f, 0.70f),
        new Coat("Blue",          0.04f, 0.35f, 0.72f, 0.30f, 0.68f),
        new Coat("Sky Blue",      0.30f, 0.62f, 0.88f, 0.15f, 0.65f),
        new Coat("Teal",          0.05f, 0.45f, 0.45f, 0.25f, 0.65f),
        new Coat("Mint",          0.55f, 0.87f, 0.72f, 0.05f, 0.50f),
        new Coat("Forest Green",  0.09f, 0.40f, 0.19f, 0.20f, 0.55f),
        new Coat("Lime",          0.62f, 0.85f, 0.15f, 0.10f, 0.50f),
        new Coat("Yellow",        0.95f, 0.82f, 0.12f, 0.10f, 0.55f),
        new Coat("Gold",          0.85f, 0.68f, 0.15f, 0.70f, 0.80f),
        new Coat("Orange",        0.90f, 0.36f, 0.06f, 0.10f, 0.55f),
        new Coat("Copper",        0.72f, 0.45f, 0.20f, 0.75f, 0.75f),
        new Coat("Racing Red",    0.82f, 0.11f, 0.10f, 0.20f, 0.72f),
        new Coat("Crimson",       0.58f, 0.06f, 0.12f, 0.15f, 0.70f),
        new Coat("Rose",          0.87f, 0.45f, 0.55f, 0.15f, 0.60f),
        new Coat("Magenta",       0.86f, 0.10f, 0.60f, 0.20f, 0.65f),
        new Coat("Purple",        0.35f, 0.14f, 0.55f, 0.35f, 0.70f),
        new Coat("Beige",         0.86f, 0.80f, 0.66f, 0.05f, 0.45f),
        new Coat("Brown",         0.36f, 0.24f, 0.15f, 0.20f, 0.50f),
    };

    /// <summary>The palette as materials, made in the project if they are not there yet.</summary>
    public static Material[] Ensure()
    {
        Material template = AssetDatabase.LoadAssetAtPath<Material>(TemplatePath);

        EnsureFolder(Folder);

        List<Material> materials = new List<Material>(Coats.Length);
        bool created = false;

        for (int i = 0; i < Coats.Length; i++)
        {
            Coat coat = Coats[i];
            string path = Folder + "/CarColor " + coat.name + ".mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = template != null ? new Material(template) : new Material(DefaultShader());
                material.name = "CarColor " + coat.name;
                Paint(material, coat);

                AssetDatabase.CreateAsset(material, path);
                created = true;
            }

            if (material != null) materials.Add(material);
        }

        if (created) AssetDatabase.SaveAssets();

        return materials.ToArray();
    }

    private static void Paint(Material material, Coat coat)
    {
        // Standard calls the diffuse _Color and the finish _Metallic/_Glossiness; the scriptable pipelines
        // call them _BaseColor and _Smoothness. Setting whichever the shader has keeps this working if the
        // project ever moves renderer.
        if (material.HasProperty(ColorId)) material.SetColor(ColorId, coat.color);
        if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, coat.color);
        if (material.HasProperty(MetallicId)) material.SetFloat(MetallicId, coat.metallic);
        if (material.HasProperty(GlossinessId)) material.SetFloat(GlossinessId, coat.smoothness);
        if (material.HasProperty(SmoothnessId)) material.SetFloat(SmoothnessId, coat.smoothness);
    }

    private static Shader DefaultShader()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");

        return shader;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return;

        folder = folder.Replace('\\', '/');
        if (folder == "Assets" || AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
