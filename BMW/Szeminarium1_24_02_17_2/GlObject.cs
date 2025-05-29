using Silk.NET.OpenGL;
using System.Collections.Generic;
using System.Linq;

namespace Szeminarium1_24_02_17_2
{
    internal class GlObject
    {
        public GL Gl { get; }
        public uint VAO { get; }
        public uint VBO { get; }
        public uint EBO { get; }
        public uint IndexCount { get; }
        public Dictionary<string, List<(uint Start, uint Count)>> MaterialRanges { get; set; }

        public GlObject(GL gl, uint vao, uint vbo, uint ebo, uint indexCount)
        {
            Gl = gl;
            VAO = vao;
            VBO = vbo;
            EBO = ebo;
            IndexCount = indexCount;
            MaterialRanges = new Dictionary<string, List<(uint, uint)>>();
        }

        public unsafe void Draw()
        {
            Gl.BindVertexArray(VAO);
            Gl.DrawElements(PrimitiveType.Triangles, IndexCount, DrawElementsType.UnsignedInt, null);
        }

        public void ReleaseGlObject()
        {
            Gl.DeleteVertexArray(VAO);
            Gl.DeleteBuffer(VBO);
            Gl.DeleteBuffer(EBO);
        }

        public unsafe void DrawWithMaterials()
        {
            Gl.BindVertexArray(VAO);

            foreach (var materialEntry in MaterialRanges)
            {
                string materialName = materialEntry.Key;
                foreach (var range in materialEntry.Value)
                {
                    Gl.DrawElements(
                        PrimitiveType.Triangles,
                        range.Count,
                        DrawElementsType.UnsignedInt,
                        (void*)(range.Start * sizeof(uint))
                        );
                }
            }
        }
    }
}