using Silk.NET.OpenGL;

namespace Szeminarium1_24_02_17_2
{
    public class GlObject : IDisposable
    {
        private readonly GL _gl;
        public uint Vao { get; }
        public uint Vbo { get; }
        public uint Ebo { get; }
        public uint IndexArrayLength { get; }

        public GlObject(GL gl, uint vao, uint vbo, uint ebo, uint indexArrayLength)
        {
            _gl = gl;
            Vao = vao;
            Vbo = vbo;
            Ebo = ebo;
            IndexArrayLength = indexArrayLength;
        }

        public static unsafe GlObject Create(GL gl, float[] vertices, uint[] indices)
        {
            uint vao = gl.GenVertexArray();
            gl.BindVertexArray(vao);

            uint vbo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            fixed (float* v = &vertices[0])
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, GLEnum.StaticDraw);
            }

            uint ebo = gl.GenBuffer();
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            fixed (uint* i = &indices[0])
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), i, GLEnum.StaticDraw);
            }

            gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 9 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);

            gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 9 * sizeof(float), (void*)(3 * sizeof(float)));
            gl.EnableVertexAttribArray(1);

            gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 9 * sizeof(float), (void*)(7 * sizeof(float)));
            gl.EnableVertexAttribArray(2);

            gl.BindVertexArray(0); // Unbind the VAO

            return new GlObject(gl, vao, vbo, ebo, (uint)indices.Length);
        }

        public void ReleaseGlObject()
        {
            _gl.DeleteVertexArray(Vao);
            _gl.DeleteBuffer(Vbo);
            _gl.DeleteBuffer(Ebo);
        }

        public void Dispose()
        {
            ReleaseGlObject();
            GC.SuppressFinalize(this);
        }
    }
}
