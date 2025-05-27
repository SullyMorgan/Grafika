using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Szeminarium1_24_02_17_2
{
    internal static class Program
    {
        private static CameraDescriptor cameraDescriptor = new();


        private static IWindow? window;

        private static IInputContext? inputContext;

        private static GL? Gl;

        private static ImGuiController? controller;

        private static uint program;

        private static GlObject? car;

        private static Vector3D<float> carPosition = new Vector3D<float>(0f, 0f, 0f);
        private static Vector3D<float> carDirection = new Vector3D<float>(0f, 0f, 1f);
        private static float carRotation = 0f; // in radians

        private static float Shininess = 50;

        private const string ModelMatrixVariableName = "model";
        private const string NormalMatrixVariableName = "normal";
        private const string ViewMatrixVariableName = "view";
        private const string ProjectionMatrixVariableName = "projection";

        private static bool isUserInputDetected = false;

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPos;
        layout (location = 1) in vec3 aNormal;
        layout (location = 2) in vec3 aColor;

        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        uniform mat3 normal;

        out vec3 FragPos;
        out vec3 Normal;
        out vec3 Color;

        void main()
        {
            vec4 worldPosition = model * vec4(aPos, 1.0);
            FragPos = vec3(worldPosition);
            gl_Position = projection * view * vec4(FragPos, 1.0);

            Normal = normalize(normal * aNormal);
            Color = aColor;
        }
        ";

        private const string LightColorVariableName = "lightColor";
        private const string LightPositionVariableName = "lightPos";
        private const string ViewPosVariableName = "viewPos";
        private const string ShininessVariableName = "shininess";

        private static readonly string FragmentShaderSource = @"
        #version 330 core

        in vec3 FragPos;
        in vec3 Normal;
        in vec3 Color;

        out vec4 FragColor;

        uniform vec3 lightPos;
        uniform vec3 viewPos;
        uniform vec3 lightColor;
        uniform float shininess;

        void main()
        {
            // Ambient
            float ambientStrength = 0.2;
            vec3 ambient = ambientStrength * lightColor;

            // Diffuse
            vec3 norm = normalize(Normal);
            vec3 lightDir = normalize(lightPos - FragPos);
            float diff = max(dot(norm, lightDir), 0.0);
            vec3 diffuse = diff * lightColor;

            // Specular
            float specularStrength = 0.5;
            vec3 viewDir = normalize(viewPos - FragPos);
            vec3 reflectDir = reflect(-lightDir, norm);
            float spec = pow(max(dot(viewDir, reflectDir), 0.0), shininess);
            vec3 specular = specularStrength * spec * lightColor;

            vec3 result = (ambient + diffuse + specular) * Color;
            FragColor = vec4(result, 1.0);
        }
        ";

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "2 szeminárium";
            windowOptions.Size = new Vector2D<int>(500, 500);

            // on some systems there is no depth buffer by default, so we need to make sure one is created
            windowOptions.PreferredDepthBufferBits = 24;

            window = Window.Create(windowOptions);

            window.Load += Window_Load;
            Console.WriteLine("Window_Load lefutott.");
            window.Update += Window_Update;
            Console.WriteLine("Window_Upadte lefutott.");
            window.Render += Window_Render;
            Console.WriteLine("Window_Render lefutott.");
            window.Closing += Window_Closing;
            Console.WriteLine("Window_Closing lefutott.");

            window.Run();
        }

        private static class MathHelper
        {
            public static float DegreesToRadians(float degrees)
            {
                return degrees * (MathF.PI / 180f);
            }
        }

        private static void Window_Load()
        {
            //Console.WriteLine("Load");

            // set up input handling
            Console.WriteLine("Initializing OpenGL...");

            Gl = window.CreateOpenGL();
            if (Gl == null)
            {
                Console.WriteLine("Failed to initialize OpenGL");
            }
            else
            {
                Console.WriteLine("OpenGL initialized successfully");
                Console.WriteLine($"GL version: {Gl.GetStringS(StringName.Version)}");
            }
            inputContext = window.CreateInput();
            if (inputContext == null)
            {
                Console.WriteLine("Failed to create input context");
            }
            else
            {
                Console.WriteLine("Input contex created.");
            }
            CheckError("ha a loadban is van error beleverem a faszomat");
            controller = new ImGuiController(Gl, window, inputContext);
            Console.WriteLine("ImGui controller created.");

            LinkProgram();
            Gl.UseProgram(program);
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }

            // Handle resizes
            window.FramebufferResize += s =>
            {
                // Adjust the viewport to the new window size
                Gl.Viewport(s);
            };

            car = ObjResourceReader.CreateFromObjWithMaterials(
                Gl,
                "Szeminarium1_24_02_17_2.Resources.car.obj",
                "Szeminarium1_24_02_17_2.Resources.car.mtl"
                );

            Gl.ClearColor(System.Drawing.Color.Black);

            Gl.Enable(EnableCap.CullFace);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);
            CheckError("ha a load vegen is van error beleverem a faszomat");
        }

        private static unsafe void SetMaterialUniforms(Material material)
        {
            int locColor = Gl.GetUniformLocation(program, "uMaterialColor");
            if (locColor == -1)
            {
                throw new Exception("uMaterialColor uniform not found on shader.");
            }

            Gl.Uniform3(locColor, material.DiffuseColor.X, material.DiffuseColor.Y, material.DiffuseColor.Z);

            int locShine = Gl.GetUniformLocation(program, "shininess");
            if (locShine == -1)
            {
                throw new Exception("shininess uniform not found on shader.");
            }
            Gl.Uniform1(locShine, material.Shininess);
        }

        private static void LinkProgram()
        {
            Console.WriteLine("Linking shaders...");
            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader);
            Gl.GetShader(fshader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
                throw new Exception("Fragment shader failed to compile: " + Gl.GetShaderInfoLog(fshader));

            
            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);
            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                throw new Exception($"Error linking shader {Gl.GetProgramInfoLog(program)}");
            }
            Console.WriteLine("Shaders compiled successfully.");
            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);
        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            const float moveSpeed = 1.0f;
            const float rotationSpeed = 5.0f;
            switch (key)
            {
                case Key.W:
                    isUserInputDetected = true;
                    carPosition += carDirection * moveSpeed;
                    break;
                    ;
                case Key.S:
                    isUserInputDetected = true;
                    carPosition -= carDirection * moveSpeed;
                    break;
                case Key.A:
                    isUserInputDetected = true;
                    carRotation += rotationSpeed;
                    UpdateCarDirection(false);
                    break;
                case Key.D:
                    isUserInputDetected = true;
                    carRotation -= rotationSpeed;
                    UpdateCarDirection(true);
                    break;
            }
        }

        private static void UpdateCarDirection(bool direction)
        {
            if (direction)
            {
                carDirection = new Vector3D<float>(
                MathF.Sin(MathHelper.DegreesToRadians(carRotation)),
                0,
                MathF.Cos(MathHelper.DegreesToRadians(carRotation))
                );
            }
            else
            {
                carDirection = new Vector3D<float>(
                MathF.Cos(MathHelper.DegreesToRadians(-carRotation)),
                0,
                MathF.Sin(MathHelper.DegreesToRadians(-carRotation))
                );
            }

        }

        private static void UpdateCamera()
        {
            if (isUserInputDetected)
            {
                cameraDescriptor.Target = carPosition;

                var cameraPosition = cameraDescriptor.Position;
                var cameraTarget = cameraDescriptor.Target;
                var upVector = cameraDescriptor.UpVector;

                var viewMatrix = Matrix4X4.CreateLookAt(cameraPosition, cameraTarget, upVector);

                Console.WriteLine($"Camera position: {cameraPosition}, cameraTarget: {cameraTarget}, upVector: {upVector}");

                CheckError("B4 SetViewMatrix");
                SetViewMatrix(viewMatrix);
                CheckError("After SetViewMatrix");
                isUserInputDetected = false;
            }
        }

        private static void Window_Update(double deltaTime)
        {
            //CheckError("window update megszop");
            while (Gl.GetError() != GLEnum.NoError) { }
            if (isUserInputDetected)
            {
                UpdateCamera();
                //UpdateCarDirection();
            }

            //controller.Update((float)deltaTime);
        }

        private static unsafe void Window_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s].");

            // GL here
            while (Gl.GetError() != GLEnum.NoError) { }
            controller.Update((float)deltaTime);
            Console.WriteLine("Rendering frame...");
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);


            //Gl.UseProgram(program);
            Console.WriteLine("Using shader program...");

            if (program == 0)
            {
                Console.WriteLine("Shader program is 0! Ez baj lesz");
            }

            Vector3D<float> forward = Vector3D.Normalize(carDirection);
            Vector3D<float> up = new Vector3D<float>(0f, 1f, 0f);
            float distanceBack = 10f;
            float heightUp = 3f;
            Vector3D<float> cameraPosition = carPosition + new Vector3D<float>(0f, heightUp, -distanceBack);
            CheckError("after cameraPosition calculation");
            var viewMatrix = Matrix4X4.CreateLookAt(cameraPosition, carPosition, up);
            
            Console.WriteLine("View matrix set.");

            CheckError("B4 SetViewMatrix in Window_Render()");
            SetViewMatrix(viewMatrix);
            CheckError("After SetViewMatrix in Window_Render()");
            SetProjectionMatrix();

            SetLightColor();
            SetLightPosition();
            SetViewerPosition();
            SetShininess();

            DrawCar();
            Console.WriteLine("Car drawn.");

            //ImGuiNET.ImGui.ShowDemoWindow();
            ImGuiNET.ImGui.Begin("Lighting properties",
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
            ImGuiNET.ImGui.SliderFloat("Shininess", ref Shininess, 1, 200);
            ImGuiNET.ImGui.End();


            controller.Render();
            Console.WriteLine("ImGui rendered.");
        }

        private static unsafe void SetLightColor()
        {
            Console.WriteLine("Setting light color...");
            int location = Gl.GetUniformLocation(program, LightColorVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightColorVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, 1f, 1f, 1f);
            CheckError("setlightcolor");
        }

        private static unsafe void SetLightPosition()
        {
            Console.WriteLine("Setting light position...");
            int location = Gl.GetUniformLocation(program, LightPositionVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightPositionVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, 0f, 10f, 0f);
            CheckError("setlightposition");
        }

        private static unsafe void SetViewerPosition()
        {
            Console.WriteLine("Setting viewer position...");
            int location = Gl.GetUniformLocation(program, ViewPosVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewPosVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, cameraDescriptor.Position.X, cameraDescriptor.Position.Y, cameraDescriptor.Position.Z);
            CheckError("SetViewerPosition");
        }

        private static unsafe void SetShininess()
        {
            int location = Gl.GetUniformLocation(program, ShininessVariableName);

            if (location == -1)
            {
                throw new Exception($"{ShininessVariableName} uniform not found on shader.");
            }

            Gl.Uniform1(location, Shininess);
            CheckError("SetShininess");
        }

        private static unsafe void DrawCar()
        {
            // set material uniform to rubber
            var modelMatrix = Matrix4X4<float>.Identity;

            var rotationMatrix = Matrix4X4.CreateRotationY(MathHelper.DegreesToRadians(carRotation));
            var translationMatrix = Matrix4X4.CreateTranslation(carPosition);
            modelMatrix = translationMatrix * rotationMatrix;

            SetModelMatrix(modelMatrix);

            car.DrawWithMaterials();
        }

        private static unsafe void SetModelMatrix(Matrix4X4<float> modelMatrix)
        {
            Gl.UseProgram(program);
            int location = Gl.GetUniformLocation(program, ModelMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{ModelMatrixVariableName} uniform not found on shader.");
            }

            Console.WriteLine($"Uniform location: {location}");
            Console.WriteLine($"Model matrix: {modelMatrix}");

            Gl.UniformMatrix4(location, 1, false, (float*)&modelMatrix);
            CheckError("SetModelMatrix1");

            var modelMatrixWithoutTranslation = new Matrix4X4<float>(modelMatrix.Row1, modelMatrix.Row2, modelMatrix.Row3, modelMatrix.Row4);
            modelMatrixWithoutTranslation.M41 = 0;
            modelMatrixWithoutTranslation.M42 = 0;
            modelMatrixWithoutTranslation.M43 = 0;
            modelMatrixWithoutTranslation.M44 = 1;

            Matrix4X4<float> modelInvers;
            Matrix4X4.Invert<float>(modelMatrixWithoutTranslation, out modelInvers);
            Matrix3X3<float> normalMatrix = new Matrix3X3<float>(Matrix4X4.Transpose(modelInvers));
            location = Gl.GetUniformLocation(program, NormalMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{NormalMatrixVariableName} uniform not found on shader.");
            }
            Gl.UniformMatrix3(location, 1, false, (float*)&normalMatrix);
            CheckError("SetModelMatrix2");
        }

        private static void Window_Closing()
        {
            car.ReleaseGlObject();
        }

        private static unsafe void SetProjectionMatrix()
        {
            if (window.Size.X == 0 || window.Size.Y == 0)
            {
                throw new Exception("Window size is invalid.");
            }
            float aspectRatio = (float)window.Size.X / (float)window.Size.Y;
            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)Math.PI / 4f, aspectRatio, 0.1f, 100);
            int location = Gl.GetUniformLocation(program, ProjectionMatrixVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&projectionMatrix);
            CheckError("SetProjectionMatrix");
        }

        private static unsafe void SetViewMatrix(Matrix4X4<float> viewMatrix)
        {
            Console.WriteLine("Checking whats in SetViewMatrix()");
            int viewMatrixLocation = Gl.GetUniformLocation(program, ViewMatrixVariableName);

            if (viewMatrixLocation == -1)
            {
                throw new Exception($"Uniform {ViewMatrixVariableName} not found on the shader.");
            }

            Gl.UniformMatrix4(viewMatrixLocation, 1, false, (float*)&viewMatrix);
            Console.WriteLine($"View matrix: {viewMatrix}");
            CheckError("SetViewMatrix");
        }

        public static void CheckError(string context = "Unknown context")
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
            {
                Console.WriteLine($"OpenGL error in {context}: {error}");
                throw new Exception($"OpenGL error in {context}: {error}");
            }
        }
    }
}