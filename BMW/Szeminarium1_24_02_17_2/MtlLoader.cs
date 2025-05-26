using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace Szeminarium1_24_02_17_2
{
    internal class Material
    {
        public string Name { get; set; } = "";
        public Vector3 DiffuseColor { get; set; } = Vector3.One;
        public Vector3 AmbientColor { get; set; } = Vector3.One;
        public Vector3 SpecularColor { get; set; } = Vector3.One;
        public float Shininess { get; set; } = 32f;
    }

    internal static class MtlLoader
    {
        public static Dictionary<string, Material> Load(string path)
        {
            var materials = new Dictionary<string, Material>();
            Material? currentMaterial = null;

            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                {
                    continue;
                }

                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                switch (parts[0])
                {
                    case "newmtl":
                        currentMaterial = new Material { Name = parts[1] };
                        materials[currentMaterial.Name] = currentMaterial;
                        break;
                    case "Kd":
                        if (currentMaterial != null)
                            currentMaterial.AmbientColor = ParseVector3(parts);
                        break;
                    case "Ka":
                        if (currentMaterial != null)
                            currentMaterial.AmbientColor = ParseVector3(parts);
                        break;
                    case "Ks":
                        if (currentMaterial != null)
                            currentMaterial.SpecularColor = ParseVector3(parts);
                        break;
                    case "Ns":
                        if (currentMaterial != null)
                            currentMaterial.Shininess = float.Parse(parts[1]);
                        break;
                }
            }

            return materials;
        }

        private static Vector3 ParseVector3(string[] parts)
        {
            float x = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            float y = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            float z = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
            return new Vector3(x, y, z);
        }
    }
}
