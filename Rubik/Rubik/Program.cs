using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;

namespace Rubik
{
    internal static class Program
    {
        private static IWindow window;
        private static GL Gl;
        private static uint program;

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
		layout (location = 1) in vec4 vCol;

		out vec4 outCol;
        
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;

        void main()
        {
			outCol = vCol;
            gl_Position = projection * view * model * vec4(vPos, 1.0);
        }
        ";


        private static readonly string FragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;
		
		in vec4 outCol;

        void main()
        {
            FragColor = outCol;
        }
        ";

        private static readonly float[] CubeVertices = new float[]
        {
            // eleje
            -0.5f, -0.5f, 0.5f, // 0
             0.5f, -0.5f, 0.5f, // 1
             0.5f,  0.5f, 0.5f, // 2
            -0.5f,  0.5f, 0.5f, // 3

            // jobb
             0.5f, -0.5f,  0.5f, // 4
             0.5f, -0.5f, -0.5f, // 5
             0.5f,  0.5f, -0.5f, // 6
             0.5f,  0.5f,  0.5f, // 7

            // hatulja
             0.5f, -0.5f, -0.5f, // 8
            -0.5f, -0.5f, -0.5f, // 9
            -0.5f,  0.5f, -0.5f, // 10
             0.5f,  0.5f, -0.5f, // 11

            // bal
            -0.5f, -0.5f, -0.5f, // 12
            -0.5f, -0.5f,  0.5f, // 13
            -0.5f,  0.5f,  0.5f, // 14
            -0.5f,  0.5f, -0.5f, // 15

            // teteje
            -0.5f, 0.5f,  0.5f, // 16
             0.5f, 0.5f,  0.5f, // 17
             0.5f, 0.5f, -0.5f, // 18
            -0.5f, 0.5f, -0.5f, // 19

            // alja
            -0.5f, -0.5f, -0.5f, // 20
             0.5f, -0.5f, -0.5f, // 21
             0.5f, -0.5f,  0.5f, // 22
            -0.5f, -0.5f,  0.5f // 23
        };

        private static readonly uint[] CubeIndices = new uint[]
        {
            0, 1, 2, 2, 3, 0,   // eleje
            4, 5, 6, 6, 7, 4,   // jobb
            8, 9, 10, 10, 11, 8,   // hatulja
            12, 13, 14, 14, 15, 12,   // bal
            16, 17, 18, 18, 19, 16,   // teteje
            20, 21, 22, 22, 23, 20    // alja
        };

        private struct Cube
        {
            public uint VAO;
            public uint ColorBuffer;
            public Vector3 Position;
        }

        private static List<Cube> Cubes = new();
        private static uint VertexBuffer, IndexBuffer;

        static void Main(string[] args)
        {
            var options = WindowOptions.Default;
            options.Title = "Rubik Kocka";
            options.Size = new Silk.NET.Maths.Vector2D<int>(800, 800);

            window = Window.Create(options);

            window.Load += GraphicWindow_Load;
            window.Render += GraphicWindow_Render;

            window.Run();
        }

        private static unsafe void GraphicWindow_Load()
        {
            Gl = window.CreateOpenGL();
            Gl.ClearColor(1f, 1f, 1f, 1f);

            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);
            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader);

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

            VertexBuffer = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, VertexBuffer);
            fixed (float* v = CubeVertices)
                Gl.BufferData(GLEnum.ArrayBuffer, (uint)(CubeVertices.Length * sizeof(float)), v, GLEnum.StaticDraw);

            IndexBuffer = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, IndexBuffer);
            fixed (uint* i = CubeIndices)
                Gl.BufferData(GLEnum.ElementArrayBuffer, (uint)(CubeIndices.Length * sizeof(uint)), i, GLEnum.StaticDraw);

            float spacing = 1.1f;
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        Cubes.Add(CreateCube(new Vector3(x * spacing, y * spacing, z * spacing)));
        }

        private static unsafe Cube CreateCube(Vector3 position)
        {
            uint vao = Gl.GenVertexArray();
            Gl.BindVertexArray(vao);

            Gl.BindBuffer(GLEnum.ArrayBuffer, VertexBuffer);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(0);

            float[] colors = GenerateColors(position);
            uint colorBuffer = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, colorBuffer);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)colors, GLEnum.StaticDraw);
            Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
            Gl.EnableVertexAttribArray(1);

            return new Cube
            {
                VAO = vao,
                ColorBuffer = colorBuffer,
                Position = position
            };
        }

        private static float[] GenerateColors(Vector3 pos)
        {
            Vector4 gray = new(0.2f, 0.2f, 0.2f, 1.0f);
            Vector4 red = new(1.0f, 0.0f, 0.0f, 1.0f);
            Vector4 green = new(0.0f, 1.0f, 0.0f, 1.0f);
            Vector4 blue = new(0.0f, 0.0f, 1.0f, 1.0f);
            Vector4 yellow = new(1.0f, 1.0f, 0.0f, 1.0f);
            Vector4 white = new(1.0f, 1.0f, 1.0f, 1.0f);
            Vector4 orange = new(1.0f, 0.5f, 0.0f, 1.0f);

            Vector4[] faceColors = new Vector4[6];
            faceColors[0] = pos.Z > 0.5 ? green : gray;
            faceColors[1] = pos.X > 0.5 ? red : gray;
            faceColors[2] = pos.Z < -0.5 ? blue : gray;
            faceColors[3] = pos.X < -0.5 ? orange : gray;
            faceColors[4] = pos.Y > 0.5 ? white : gray;
            faceColors[5] = pos.Y < -0.5 ? yellow : gray;

            List<float> colorList = new();
            foreach (var face in faceColors)
                for (int i = 0; i < 4; i++)
                    colorList.AddRange(new[] { face.X, face.Y, face.Z, face.W });

            return colorList.ToArray();
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Gl.Enable(GLEnum.DepthTest);

            Gl.UseProgram(program);

            Matrix4x4 view = Matrix4x4.CreateLookAt(
                new Vector3(5, 5, 5),
                Vector3.Zero,
                Vector3.UnitY
                );

            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI / 4,
                1f,
                0.1f,
                100f
                );

            int viewLocation = Gl.GetUniformLocation(program, "view");
            int projectionLocation = Gl.GetUniformLocation(program, "projection");

            Gl.UniformMatrix4(viewLocation, 1, false, (float*)&view);
            Gl.UniformMatrix4(projectionLocation, 1, false, (float*)&projection);

            foreach (var cube in Cubes)
            {
                Gl.BindVertexArray(cube.VAO);
                Gl.BindBuffer(GLEnum.ElementArrayBuffer, IndexBuffer);

                Matrix4x4 model = Matrix4x4.CreateTranslation(cube.Position);
                int modelLocation = Gl.GetUniformLocation(program, "model");
                Gl.UniformMatrix4(modelLocation, 1, false, (float*)&model);

                Gl.DrawElements(
                    GLEnum.Triangles,
                    (uint)CubeIndices.Length,
                    GLEnum.UnsignedInt,
                    null
                );
            }
        }
    }
}