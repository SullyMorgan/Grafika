using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace Projekt
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
        public static Dictionary<string, Material> LoadFromResource(string resourcePath)
        {
            var materials = new Dictionary<string, Material>();
            Material currentMaterial = null;

            using (Stream stream = typeof(MtlLoader).Assembly.GetManifestResourceStream(resourcePath)
                ?? throw new FileNotFoundException("MTL resource not found", resourcePath))
            using (StreamReader reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0)
                        continue;

                    ProcessMtlLine(parts, materials, ref currentMaterial);
                }
            }

            return materials;
        }

        private static void ProcessMtlLine(string[] parts, Dictionary<string, Material> materials, ref Material currentMaterial)
        {
            switch (parts[0])
            {
                case "newmtl":
                    currentMaterial = new Material { Name = parts[1] };
                    materials[currentMaterial.Name] = currentMaterial;
                    break;
                case "Kd":
                    if (currentMaterial != null)
                        currentMaterial.DiffuseColor = ParseVector3(parts);
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
                        currentMaterial.Shininess = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
            }
        }

        private static Vector3 ParseVector3(string[] parts)
        {
            float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
            float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
            float z = float.Parse(parts[3], CultureInfo.InvariantCulture);
            return new Vector3(x, y, z);
        }
    }
}