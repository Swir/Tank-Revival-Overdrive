using System.Collections.Generic;
using UnityEngine;

namespace TankRevival
{
    /// <summary>
    /// Lightweight runtime 3D mesh/material factory used by the 3D migration layer.
    /// It deliberately creates MeshFilter/MeshRenderer objects only: the proven
    /// Rigidbody2D/Collider2D simulation remains the sole gameplay authority.
    /// </summary>
    public static class Runtime3DFactory
    {
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();
        private static Mesh _cubeMesh;
        private static Mesh _cylinderMesh;

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
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetCubeMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(color, metallic, smoothness);
            return go;
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 localPosition, float diameter, float depth, Color color, float metallic = 0.15f, float smoothness = 0.42f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(diameter, diameter, depth);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetCylinderMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material(color, metallic, smoothness);
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

        private static Mesh GetCubeMesh()
        {
            if (_cubeMesh != null) return _cubeMesh;

            Vector3[] vertices =
            {
                // Front (-Z)
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                // Back (+Z)
                new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(-0.5f, -0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f),
                // Left
                new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f,  0.5f),
                // Right
                new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f, -0.5f),
                // Bottom
                new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
                // Top
                new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f)
            };

            int[] triangles =
            {
                 0, 2, 1,  0, 3, 2,
                 4, 6, 5,  4, 7, 6,
                 8,10, 9,  8,11,10,
                12,14,13, 12,15,14,
                16,18,17, 16,19,18,
                20,22,21, 20,23,22
            };

            _cubeMesh = new Mesh
            {
                name = "Runtime3D_Cube",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                triangles = triangles
            };
            _cubeMesh.RecalculateNormals();
            _cubeMesh.RecalculateBounds();
            _cubeMesh.UploadMeshData(true);
            return _cubeMesh;
        }

        private static Mesh GetCylinderMesh()
        {
            if (_cylinderMesh != null) return _cylinderMesh;

            const int segments = 18;
            var vertices = new List<Vector3>(segments * 4 + 2);
            var triangles = new List<int>(segments * 12);

            // Side wall: separate vertex ring so cap normals do not smooth into the side.
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle) * 0.5f;
                float y = Mathf.Sin(angle) * 0.5f;
                vertices.Add(new Vector3(x, y, -0.5f));
                vertices.Add(new Vector3(x, y,  0.5f));
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int a = i * 2;
                int b = next * 2;
                triangles.Add(a); triangles.Add(b + 1); triangles.Add(a + 1);
                triangles.Add(a); triangles.Add(b);     triangles.Add(b + 1);
            }

            int frontCenter = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, -0.5f));
            int frontRing = vertices.Count;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, -0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles.Add(frontCenter);
                triangles.Add(frontRing + i);
                triangles.Add(frontRing + next);
            }

            int backCenter = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, 0.5f));
            int backRing = vertices.Count;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles.Add(backCenter);
                triangles.Add(backRing + next);
                triangles.Add(backRing + i);
            }

            _cylinderMesh = new Mesh
            {
                name = "Runtime3D_Cylinder",
                hideFlags = HideFlags.HideAndDontSave
            };
            _cylinderMesh.SetVertices(vertices);
            _cylinderMesh.SetTriangles(triangles, 0);
            _cylinderMesh.RecalculateNormals();
            _cylinderMesh.RecalculateBounds();
            _cylinderMesh.UploadMeshData(true);
            return _cylinderMesh;
        }
    }
}
