using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Globalization;
using System.Numerics;

namespace Projekt
{
    internal class ObjResourceReader
    {
        public static unsafe GlObject CreateFromObjWithMaterials(GL gl, string objResourcePath, string mtlResourcePath)
        {
            uint vao = gl.GenVertexArray();
            gl.BindVertexArray(vao);

            var materials = MtlLoader.LoadFromResource(mtlResourcePath);

            var objData = ReadObjDataWithMaterials(objResourcePath);

            List<float> interleavedData = new List<float>();
            List<uint> indices = new List<uint>();
            Dictionary<string, List<(uint Start, uint Count)>> materialRanges = new Dictionary<string, List<(uint, uint)>>();

            CreateInterleavedDataWithMaterials(materials, objData, interleavedData, indices, materialRanges);

            var glObject = CreateOpenGlObject(gl, vao, interleavedData, indices);
            glObject.MaterialRanges = materialRanges;

            return glObject;
        }

        private static unsafe GlObject CreateOpenGlObject(GL gl, uint vao, List<float> interleavedData, List<uint> indices)
        {
            uint vbo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)interleavedData.ToArray().AsSpan(), GLEnum.StaticDraw);

            const int stride = 9 * sizeof(float);

            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);

            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));

            uint ebo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)indices.ToArray().AsSpan(), GLEnum.StaticDraw);

            gl.BindVertexArray(0);

            return new GlObject(gl, vao, vbo, ebo, (uint)indices.Count);
        }

        private static void CreateInterleavedDataWithMaterials(
            Dictionary<string, Material> materials,
            ObjData objData,
            List<float> interleavedData,
            List<uint> indices,
            Dictionary<string, List<(uint Start, uint Count)>> materialRanges)
        {
            Dictionary<string, uint> vertexCache = new Dictionary<string, uint>();
            uint currentIndex = 0;
            string currentMaterial = null;
            uint materialStartIndex = 0;

            for (int faceIndex = 0; faceIndex < objData.Faces.Count; faceIndex++)
            {
                var faceMaterial = objData.FaceMaterials[faceIndex];
                if (faceMaterial != currentMaterial)
                {
                    if (currentMaterial != null)
                    {
                        uint count = (uint)(indices.Count - (int)materialStartIndex);
                        if (count > 0)
                        {
                            if (!materialRanges.ContainsKey(currentMaterial))
                            {
                                materialRanges[currentMaterial] = new List<(uint, uint)>();
                            }
                            materialRanges[currentMaterial].Add((materialStartIndex, count));
                        }
                    }

                    currentMaterial = faceMaterial;
                    materialStartIndex = (uint)indices.Count;
                }
                ProcessFaceVertices(materials, objData, faceIndex, interleavedData, indices, vertexCache, ref currentIndex, faceMaterial);
            }

            if (currentMaterial != null)
            {
                uint count = (uint)(indices.Count - (int)materialStartIndex);
                if (count > 0)
                {
                    if (!materialRanges.ContainsKey(currentMaterial))
                    {
                        materialRanges[currentMaterial] = new List<(uint, uint)>();
                    }
                    materialRanges[currentMaterial].Add((materialStartIndex, count));
                }
            }
        }

        private static void ProcessFaceVertices(
            Dictionary<string, Material> materials,
            ObjData objData,
            int faceIndex,
            List<float> interleavedData,
            List<uint> indices,
            Dictionary<string, uint> vertexCache,
            ref uint currentIndex,
            string faceMaterial)
        {
            var objFace = objData.Faces[faceIndex];
            var normalIdx = objData.HasNormals ? objData.NormalIndices[faceIndex] : null;

            Vector3D<float> computedNormal = default;
            if (!objData.HasNormals)
            {
                computedNormal = ComputeFaceNormal(objData.Vertices, objFace);
            }

            for (int i = 0; i < 3; i++)
            {
                ProcessVertex(materials, objData, objFace, normalIdx, computedNormal,
                            interleavedData, indices, vertexCache, ref currentIndex,
                            faceMaterial, i);
            }
        }

        private static Vector3D<float> ComputeFaceNormal(List<float[]> vertices, int[] faceIndices)
        {
            var a = new Vector3D<float>(vertices[faceIndices[0] - 1][0], vertices[faceIndices[0] - 1][1], vertices[faceIndices[0] - 1][2]);
            var b = new Vector3D<float>(vertices[faceIndices[1] - 1][0], vertices[faceIndices[1] - 1][1], vertices[faceIndices[1] - 1][2]);
            var c = new Vector3D<float>(vertices[faceIndices[2] - 1][0], vertices[faceIndices[2] - 1][1], vertices[faceIndices[2] - 1][2]);

            return Vector3D.Normalize(Vector3D.Cross(b - a, c - a));
        }

        private static void ProcessVertex(
            Dictionary<string, Material> materials,
            ObjData objData,
            int[] faceIndices,
            int[] normalIndices,
            Vector3D<float> computedNormal,
            List<float> interleavedData,
            List<uint> indices,
            Dictionary<string, uint> vertexCache,
            ref uint currentIndex,
            string faceMaterial,
            int vertexIndex)
        {
            var vertex = objData.Vertices[faceIndices[vertexIndex] - 1];
            var normal = objData.HasNormals ?
                objData.Normals[normalIndices[vertexIndex] - 1] :
                new float[] { computedNormal.X, computedNormal.Y, computedNormal.Z };

            string key = $"{vertex[0]},{vertex[1]},{vertex[2]},{normal[0]},{normal[1]},{normal[2]}";

            if (!vertexCache.TryGetValue(key, out uint index))
            {
                Vector3 color = materials.TryGetValue(faceMaterial, out var material) ?
                               material.DiffuseColor : Vector3.One;

                interleavedData.AddRange(vertex);
                interleavedData.AddRange(normal);
                interleavedData.AddRange(new[] { color.X, color.Y, color.Z });

                vertexCache[key] = currentIndex;
                index = currentIndex;
                currentIndex++;
            }

            indices.Add(index);
        }

        private static ObjData ReadObjDataWithMaterials(string resourcePath)
        {
            //Console.WriteLine("ReadObjDataWithMaterials");
            var objData = new ObjData();
            string currentMaterial = null;

            using (Stream objStream = typeof(ObjResourceReader).Assembly.GetManifestResourceStream(resourcePath)
                ?? throw new ArgumentNullException("Resource not found"))
            using (StreamReader objReader = new StreamReader(objStream))
            {
                while (!objReader.EndOfStream)
                {
                    var line = objReader.ReadLine();
                    if (String.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#"))
                        continue;

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    ProcessObjLine(parts, objData, ref currentMaterial);
                }
            }

            return objData;
        }

        private static void ProcessObjLine(string[] parts, ObjData objData, ref string currentMaterial)
        {
            switch (parts[0])
            {
                case "v":
                    objData.Vertices.Add(new float[]
                    {
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture)
                    });
                    break;

                case "vn":
                    objData.Normals.Add(new float[]
                    {
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture)
                    });
                    break;

                case "usemtl":
                    currentMaterial = parts[1];
                    break;

                case "f":
                    ProcessFace(parts, objData, currentMaterial);
                    break;
            }
        }

        private static void ProcessFace(string[] parts, ObjData objData, string currentMaterial)
        {
            int faceVertexCount = parts.Length - 1;
            List<int> vertexIndices = new List<int>();
            List<int> normalIndices = new List<int>();
            bool normalsFound = false;

            for (int i = 1; i <= parts.Length - 1; i++)
            {
                var tokens = parts[i].Split('/');
                int vertexIndex = int.Parse(tokens[0]);
                vertexIndices.Add(vertexIndex);

                if (tokens.Length >= 3 && !string.IsNullOrWhiteSpace(tokens[2]))
                {
                    int normalIndex = int.Parse(tokens[2]);
                    normalIndices.Add(normalIndex);
                    normalsFound = true;
                }
                else
                {
                    normalIndices.Add(-1);
                }
            }

            for (int i = 0; i < faceVertexCount - 2; i++)
            {
                int[] triangleVertices = new int[]
                {
                    vertexIndices[0],
                    vertexIndices[i + 1],
                    vertexIndices[i + 2]
                };
                objData.Faces.Add(triangleVertices);

                if (normalsFound)
                {
                    int[] triangleNormals = new int[]
                    {
                        normalIndices[0],
                        normalIndices[i + 1],
                        normalIndices[i + 2]
                    };
                    objData.NormalIndices.Add(triangleNormals);
                }
                else
                {
                    objData.NormalIndices.Add(null);
                }

                objData.FaceMaterials.Add(currentMaterial);
            }

            objData.HasNormals |= normalsFound;
        }

        private class ObjData
        {
            public List<float[]> Vertices { get; } = new List<float[]>();
            public List<int[]> Faces { get; } = new List<int[]>();
            public List<float[]> Normals { get; } = new List<float[]>();
            public List<int[]> NormalIndices { get; } = new List<int[]>();
            public List<string> FaceMaterials { get; } = new List<string>();
            public bool HasNormals { get; set; }
        }
    }
}