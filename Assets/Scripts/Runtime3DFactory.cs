using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Lightweight runtime 3D primitive/material factory used by the 3D migration layer.
    /// The gameplay simulation remains Rigidbody2D/Collider2D-authoritative while these
    /// meshes provide real depth, lighting and perspective without duplicating physics.
    /// </summary>
    public static class Runtime3DFactory
    {
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();

        public static Material Material(Color color, float metallic = 0.15f, float smoothness = 0.42f)
        {
            Color32 c = color;
            int key = c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
            key = (key * 397) ^ Mathf.RoundToInt(metallic * 10f);
            key = (key * 397) ^ Mathf.RoundToInt(smoothness * 10f);
            if (Materials.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var material = new Material(shader)
            {
                name = "Runtime3D_" + key,
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);

            Materials[key] = material;
            return material;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color, float metallic = 0.15f, float smoothness = 0.42f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            RemoveCollider(go);
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = Material(color, metallic, smoothness);
            return go;
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 localPosition, float diameter, float depth, Color color, float metallic = 0.15f, float smoothness = 0.42f)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, depth * 0.5f, diameter);
            RemoveCollider(go);
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = Material(color, metallic, smoothness);
            return go;
        }

        public static void HideLegacySprites(Transform root)
        {
            if (root == null) return;
            SpriteRenderer[] sprites = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                    sprites[i].enabled = false;
            }
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
        }
    }
}
