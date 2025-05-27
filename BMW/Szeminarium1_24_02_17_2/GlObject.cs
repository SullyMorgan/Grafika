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
        public Dictionary<string, uint> MaterialRanges { get; set; }

        public GlObject(GL gl, uint vao, uint vbo, uint ebo, uint indexCount)
        {
            Gl = gl;
            VAO = vao;
            VBO = vbo;
            EBO = ebo;
            IndexCount = indexCount;
            MaterialRanges = new Dictionary<string, uint>();
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
            var materialList = MaterialRanges.ToList();

            for (int i = 0; i < materialList.Count; i++)
            {
                var current = materialList[i];
                uint start = current.Value;
                uint end = (i == materialList.Count - 1) ?
                    IndexCount : materialList[i + 1].Value;
                uint count = end - start;

                Gl.DrawElements(PrimitiveType.Triangles, count, DrawElementsType.UnsignedInt, (void*)(start * sizeof(uint)));
            }
        }
    }
}