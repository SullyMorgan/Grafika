using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Globalization;

namespace Szeminarium1_24_02_17_2
{
    internal class ObjResourceReader
    {
        public static unsafe GlObject CreateTeapotWithColor(GL Gl, float[] faceColor)
        {
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            List<float[]> objVertices;
            List<int[]> objFaces;
            List<float[]> objNormals;
            List<int[]> objNormalIndices;
            bool hasNormals;

            ReadObjDataForTeapot(out objVertices, out objFaces, out objNormals, out objNormalIndices, out hasNormals);

            List<float> glVertices = new List<float>();
            List<float> glColors = new List<float>();
            List<uint> glIndices = new List<uint>();

            CreateGlArraysFromObjArrays(faceColor, objVertices, objFaces, objNormals, objNormalIndices, hasNormals, glVertices, glColors, glIndices);

            return CreateOpenGlObject(Gl, vao, glVertices, glColors, glIndices);
        }


        private static unsafe GlObject CreateOpenGlObject(GL Gl, uint vao, List<float> glVertices, List<float> glColors, List<uint> glIndices)
        {
            uint offsetPos = 0;
            uint offsetNormal = offsetPos + (3 * sizeof(float));
            uint vertexSize = offsetNormal + (3 * sizeof(float));

            uint vertices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glVertices.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetPos);
            Gl.EnableVertexAttribArray(0);

            Gl.EnableVertexAttribArray(2);
            Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, vertexSize, (void*)offsetNormal);

            uint colors = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colors);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)glColors.ToArray().AsSpan(), GLEnum.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);

            uint indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)glIndices.ToArray().AsSpan(), GLEnum.StaticDraw);

            // release array buffer
            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            uint indexArrayLength = (uint)glIndices.Count;

            return new GlObject(vao, vertices, colors, indices, indexArrayLength, Gl);
        }

        private static unsafe void CreateGlArraysFromObjArrays(
            float[] faceColor,
            List<float[]> objVertices,
            List<int[]> objFaces,
            List<float[]> objNormals,
            List<int[]> objNormalIndices,
            bool hasNormals,
            List<float> glVertices,
            List<float> glColors,
            List<uint> glIndices)
        {
            Dictionary<string, int> glVertexIndices = new Dictionary<string, int>();

            for (int faceIndex = 0; faceIndex < objFaces.Count; faceIndex++)
            {
                var objFace = objFaces[faceIndex];
                var normalIdx = hasNormals ? objNormalIndices[faceIndex] : null;

                Vector3D<float> normal = default;

                if (!hasNormals)
                {
                    var a = new Vector3D<float>(
                        objVertices[objFace[0] - 1][0],
                        objVertices[objFace[0] - 1][1],
                        objVertices[objFace[0] - 1][2]
                    );
                    var b = new Vector3D<float>(
                        objVertices[objFace[1] - 1][0],
                        objVertices[objFace[1] - 1][1],
                        objVertices[objFace[1] - 1][2]
                    );

                    var c = new Vector3D<float>(
                        objVertices[objFace[2] - 1][0],
                        objVertices[objFace[2] - 1][1],
                        objVertices[objFace[2] - 1][2]
                    );

                    normal = Vector3D.Normalize(Vector3D.Cross(b - a, c - a));
                }

                for (int i = 0; i < 3; i++)
                {
                    var v = objVertices[objFace[i] - 1];
                    var n = hasNormals ? objNormals[normalIdx[i] - 1] : new float[] { normal.X, normal.Y, normal.Z };

                    var glVertex = new List<float>();
                    glVertex.AddRange(v); // position
                    glVertex.AddRange(n); // normal

                    var key = string.Join(" ", glVertex);
                    if (!glVertexIndices.ContainsKey(key))
                    {
                        glVertices.AddRange(glVertex);
                        glColors.AddRange(faceColor);
                        glVertexIndices.Add(key, glVertexIndices.Count);
                    }

                    glIndices.Add((uint)glVertexIndices[key]);
                }
            }
        }


        private static unsafe void ReadObjDataForTeapot(
            out List<float[]> objVertices,
            out List<int[]> objFaces,
            out List<float[]> objNormals,
            out List<int[]> objNormalIndices,
            out bool hasNormals
        )
        {
            objVertices = new List<float[]>();
            objFaces = new List<int[]>();
            objNormals = new List<float[]>();
            objNormalIndices = new List<int[]>();
            hasNormals = false;

            using (Stream objStream = typeof(ObjResourceReader).Assembly.GetManifestResourceStream("Szeminarium1_24_02_17_2.Resources.lamp.obj"))
            using (StreamReader objReader = new StreamReader(objStream))
            {
                while (!objReader.EndOfStream)
                {
                    var line = objReader.ReadLine();
                    if (String.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#"))
                        continue;

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    switch (parts[0])
                    {
                        case "v":
                            objVertices.Add(new float[]
                            {
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture)
                            });
                            break;

                        case "vn":
                            objNormals.Add(new float[]
                            {
                        float.Parse(parts[1], CultureInfo.InvariantCulture),
                        float.Parse(parts[2], CultureInfo.InvariantCulture),
                        float.Parse(parts[3], CultureInfo.InvariantCulture)
                            });
                            break;

                        case "f":
                            var vertexIndices = new int[3];
                            var normalIndices = new int[3];
                            bool normalsFound = false;

                            for (int i = 1; i <= 3; i++)
                            {
                                var tokens = parts[i].Split('/');
                                vertexIndices[i - 1] = int.Parse(tokens[0]);

                                if (tokens.Length == 3 && !string.IsNullOrWhiteSpace(tokens[2]))
                                {
                                    normalIndices[i - 1] = int.Parse(tokens[2]);
                                    normalsFound = true;
                                }
                                else if (tokens.Length == 2 && !string.IsNullOrWhiteSpace(tokens[1]))
                                {
                                    normalIndices[i - 1] = int.Parse(tokens[1]); // handle f 1//1 format
                                    normalsFound = true;
                                }
                            }

                            objFaces.Add(vertexIndices);
                            objNormalIndices.Add(normalsFound ? normalIndices : null);
                            hasNormals |= normalsFound;
                            break;
                    }
                }
            }
        }

    }
}
