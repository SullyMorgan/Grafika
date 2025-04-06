using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Rubik
{
    internal static class Program
    {
        private static IWindow window;
        private static GL Gl;
        private static uint program;
        private static IInputContext inputContext;

        private static Vector3 cameraPosition = new Vector3(5, 5, 5);
        private static Vector3 cameraFront = Vector3.Normalize(-cameraPosition);
        private static Vector3 cameraUp = Vector3.UnitY;
        private static float cameraSpeed = 0.1f;
        private static float yaw = -90f;
        private static float pitch = 0f;
        private static float lastX = 400f;
        private static float lastY = 400f;
        private static bool firstMouse = true;

        private static Matrix4x4 rotationMatrix = Matrix4x4.Identity;
        private static bool isRotating = false;
        private static float rotationAngle = 0f;
        private static Vector3 rotationAxis = Vector3.UnitX;
        private static int selectedLayer = 0;
        private static float rotationSpeed = 2f;

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
		layout (location = 1) in vec4 vCol;

		out vec4 outCol;
        
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        uniform mat4 rotation;

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
            window.Update += GraphicWindow_Update;
            window.Closing += GraphicWindow_Closing;

            window.Run();
        }

        private static unsafe void GraphicWindow_Load()
        {
            inputContext = window.CreateInput();
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }

            foreach (var mouse in inputContext.Mice)
            {
                mouse.MouseMove += Mouse_MouseMove;
                mouse.Scroll += Mouse_Scroll;
            }

            Gl = window.CreateOpenGL();
            Gl.ClearColor(0.1f, 0.1f, 0.1f, 1f);
            Gl.Enable(EnableCap.DepthTest);

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

        private static void GraphicWindow_Closing()
        {
            Gl.DeleteBuffer(VertexBuffer);
            Gl.DeleteBuffer(IndexBuffer);
            Gl.DeleteProgram(program);
            foreach (var cube in Cubes)
            {
                Gl.DeleteVertexArray(cube.VAO);
                Gl.DeleteBuffer(cube.ColorBuffer);
            }
        }

        private static void Mouse_MouseMove(IMouse mouse, System.Numerics.Vector2 position)
        {
            if (mouse.IsButtonPressed(MouseButton.Left))
            {
                if (firstMouse)
                {
                    lastX = position.X;
                    lastY = position.Y;
                    firstMouse = false;
                }

                float xoffset = position.X - lastX;
                float yoffset = lastY - position.Y;
                lastX = position.X;
                lastY = position.Y;

                float sensitivity = 0.1f;
                xoffset *= sensitivity;
                yoffset *= sensitivity;
                yaw += xoffset;
                pitch += yoffset;

                pitch = Math.Clamp(pitch, -89f, 89f);

                Vector3 front;
                front.X = MathF.Cos(MathHelper.DegreesToRadians(yaw)) * MathF.Cos(MathHelper.DegreesToRadians(pitch));
                front.Y = MathF.Sin(MathHelper.DegreesToRadians(pitch));
                front.Z = MathF.Sin(MathHelper.DegreesToRadians(yaw)) * MathF.Cos(MathHelper.DegreesToRadians(pitch));
                cameraFront = Vector3.Normalize(front);
            }
            else
            {
                firstMouse = true;
            }
        }

        private static void Mouse_Scroll(IMouse mouse, ScrollWheel scroll)
        {
            cameraSpeed += scroll.Y * 0.1f;
            cameraSpeed += Math.Clamp(cameraSpeed, 0.01f, 1f);
        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (key == Key.Escape)
            {
                window.Close();
            }

            if (key == Key.Space && selectedLayer != 0 && !isRotating)
            {
                isRotating = true;
                rotationAngle = 0f;
            }

            if (key == Key.Backspace && selectedLayer != 0 && !isRotating)
            {
                isRotating = true;
                rotationAngle = 0f;
                rotationSpeed = -rotationSpeed;
            }

            if (key >= Key.Number1 && key <= Key.Number9)
            {
                selectedLayer = (int)key - (int)Key.Number0;
            }
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            var keyboard = inputContext.Keyboards[0];
            float speed = cameraSpeed * (float)deltaTime * 60;

            if (keyboard.IsKeyPressed(Key.W))
                cameraPosition += cameraFront * speed;
            if (keyboard.IsKeyPressed(Key.S))
                cameraPosition -= cameraFront * speed;
            if (keyboard.IsKeyPressed(Key.A))
                cameraPosition -= Vector3.Normalize(Vector3.Cross(cameraFront, cameraUp)) * speed;
            if (keyboard.IsKeyPressed(Key.D))
                cameraPosition += Vector3.Normalize(Vector3.Cross(cameraFront, cameraUp)) * speed;
            if (keyboard.IsKeyPressed(Key.Q))
                cameraPosition += cameraUp * speed;
            if (keyboard.IsKeyPressed(Key.E))
                cameraPosition -= cameraUp * speed;

            if (isRotating)
            {
                float rotationStep = rotationSpeed * (float)deltaTime * 60;
                rotationAngle += rotationStep;

                if (Math.Abs(rotationAngle) >= 90f)
                {
                    isRotating = false;
                    rotationAngle = 0f;
                    rotationSpeed = Math.Abs(rotationSpeed);

                    ApplyRotationToLayer();
                    rotationMatrix = Matrix4x4.Identity;
                }
                else
                {
                    if (selectedLayer >= 1 && selectedLayer <= 3)
                        rotationAxis = Vector3.UnitX;
                    else if (selectedLayer >= 4 && selectedLayer <= 6)
                        rotationAxis = Vector3.UnitY;
                    else if (selectedLayer >= 7 && selectedLayer <= 9)
                        rotationAxis = Vector3.UnitZ;

                    rotationMatrix = Matrix4x4.CreateFromAxisAngle(rotationAxis, MathHelper.DegreesToRadians(rotationStep));
                }
            }
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

        private static void ApplyRotationToLayer()
        {
            for (int i = 0; i < Cubes.Count; i++)
            {
                var cube = Cubes[i];
                bool shouldRotate = false;
                float layerPos = 0;

                if (selectedLayer >= 1 && selectedLayer <= 3)
                {
                    layerPos = cube.Position.X;
                    shouldRotate = (selectedLayer == 1 && layerPos < -0.5f) ||
                                   (selectedLayer == 2 && Math.Abs(layerPos) < 0.5f) ||
                                   (selectedLayer == 3 && layerPos > 0.5f);
                }
                else if (selectedLayer >= 4 && selectedLayer <= 6)
                {
                    layerPos = cube.Position.Y;
                    shouldRotate = (selectedLayer == 4 && layerPos < -0.5f) ||
                                   (selectedLayer == 5 && Math.Abs(layerPos) < 0.5f) ||
                                   (selectedLayer == 6 && layerPos > 0.5f);
                }
                else if (selectedLayer >= 7 && selectedLayer <= 9)
                {
                    layerPos = cube.Position.Z;
                    shouldRotate = (selectedLayer == 7 && layerPos < -0.5f) ||
                                   (selectedLayer == 8 && Math.Abs(layerPos) < 0.5f) ||
                                   (selectedLayer == 9 && layerPos > 0.5f);
                }

                if (shouldRotate)
                {
                    // Forgatás alkalmazása
                    cube.Position = RotateVectorAroundAxis(cube.Position, rotationAxis, MathHelper.DegreesToRadians(90f) * Math.Sign(rotationSpeed));
                    Cubes[i] = cube;  // A frissített kocka pozícióját visszaírjuk
                }
            }
        }


        private static Vector3 RotateVectorAroundAxis(Vector3 position, Vector3 axis, float angle)
        {
            var rotation = Matrix4x4.CreateFromAxisAngle(axis, angle);
            return Vector3.Transform(position, rotation);
        }



        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
      
            Gl.UseProgram(program);

            Matrix4x4 view = Matrix4x4.CreateLookAt(
                cameraPosition,
                cameraPosition + cameraFront,
                cameraUp
                );

            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI / 4,
                (float)window.Size.X / window.Size.Y,
                0.1f,
                100f
                );

            int viewLocation = Gl.GetUniformLocation(program, "view");
            int projectionLocation = Gl.GetUniformLocation(program, "projection");
            int rotationLocation = Gl.GetUniformLocation(program, "rotation");

            Gl.UniformMatrix4(viewLocation, 1, false, (float*)Unsafe.AsPointer(ref view.M11));
            Gl.UniformMatrix4(projectionLocation, 1, false, (float*)Unsafe.AsPointer(ref projection.M11));


            foreach (var cube in Cubes)
            {
                Gl.BindVertexArray(cube.VAO);
                Gl.BindBuffer(GLEnum.ElementArrayBuffer, IndexBuffer);

                Matrix4x4 model = Matrix4x4.CreateTranslation(cube.Position);
                int modelLocation = Gl.GetUniformLocation(program, "model");
                Gl.UniformMatrix4(modelLocation, 1, false, (float*)&model);

                bool shouldRotate = false;
                float layerPos = 0;

                if (selectedLayer >= 1 && selectedLayer <= 3)
                {
                    layerPos = cube.Position.X;
                    shouldRotate = (selectedLayer == 1 && layerPos < -0.5f) ||
                                   (selectedLayer == 2 && Math.Abs(layerPos) < 0.5f) ||
                                   (selectedLayer == 3 && layerPos > 0.5f);
                }
                else if (selectedLayer >= 4 && selectedLayer <= 6)
                {
                    layerPos = cube.Position.Y;
                    shouldRotate = (selectedLayer == 4 && layerPos < -0.5f) ||
                                   (selectedLayer == 5 && Math.Abs(layerPos) < 0.5f) ||
                                   (selectedLayer == 6 && layerPos > 0.5f);
                }
                else if (selectedLayer >= 7 && selectedLayer <= 9)
                {
                    layerPos = cube.Position.Z;
                    shouldRotate = (selectedLayer == 7 && layerPos < -0.5f) ||
                                   (selectedLayer == 8 && Math.Abs(layerPos) < 0.5f) ||
                                   (selectedLayer == 9 && layerPos > 0.5f);
                }

                if (shouldRotate && isRotating)
                {
                    Gl.UniformMatrix4(rotationLocation, 1, false, (float*)Unsafe.AsPointer(ref rotationMatrix.M11));
                }
                else
                {
                    Matrix4x4 identity = Matrix4x4.Identity;
                    Gl.UniformMatrix4(rotationLocation, 1, false, (float*)&identity);
                }

                Gl.DrawElements(
                    GLEnum.Triangles,
                    (uint)CubeIndices.Length,
                    GLEnum.UnsignedInt,
                    null);
            }
        }
    }

    public static class MathHelper
    {
        public static float DegreesToRadians(float degrees)
        {
            return degrees * (MathF.PI / 180f);
        }
    }
}