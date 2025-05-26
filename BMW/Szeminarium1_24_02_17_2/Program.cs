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


        private static IWindow window;

        private static IInputContext inputContext;

        private static GL Gl;

        private static ImGuiController controller;

        private static uint program;

        private static GlObject teapot;

        private static Dictionary<string, Material> materials;

        private static Material currentMaterial;

        private static List<Vector3D<float>> roadPoints = new List<Vector3D<float>>();

        private static int currentRoadSegment = 0;

        private static float progressOnSegment = 0f;

        private static float carSpeed = 5f; // egyseg/mp
        
        private static Vector3D<float> carPosition = new Vector3D<float>(0f, 0f, 0f);
        private static Vector3D<float> carDirection = new Vector3D<float>(0f, 0f, 1f);
        private static float carRotation = 0f; // in radians

        private static GlObject road;
        private static List<GlObject> buildings = new List<GlObject>();
        private static float[] face1Color = { 1f, 0f, 0f, 1.0f };

        private static float Shininess = 50;

        private const string ModelMatrixVariableName = "model";
        private const string NormalMatrixVariableName = "normal";
        private const string ViewMatrixVariableName = "view";
        private const string ProjectionMatrixVariableName = "projection";

        private static bool isUserInputDetected = false;

        // skybox
        private static uint skyboxVAO, skyboxVBO;
        private static uint skyboxProgram;
        private static uint cubemapTexture;

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPos;
        layout (location = 1) in vec4 aColor;
        layout (location = 2) in vec3 aNormal;

        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        uniform mat3 normal;

        out vec3 FragPos;
        out vec3 Normal;
        out vec4 vertexColor;
        out vec3 TexCoords;

        void main()
        {
            TexCoords = aPos;
            FragPos = vec3(model * vec4(aPos, 1.0));
            Normal = normalize(normal * aNormal);
            vertexColor = aColor;
            gl_Position = projection * view * vec4(FragPos, 1.0);
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
        in vec4 vertexColor;
        in vec3 TexCoords;

        out vec4 FragColor;

        uniform vec3 lightPos;
        uniform vec3 viewPos;
        uniform vec3 lightColor;
        uniform float shininess;
        uniform vec3 uMaterialColor;

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

            vec3 result = (ambient + diffuse + specular) * uMaterialColor;
            FragColor = vec4(result, 1.0);
        }
        ";

        private static readonly string SkyboxVertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPos;

        out vec3 TexCoords;

        uniform mat4 projection;
        uniform mat4 view;

        void main()
        {
            TexCoords = aPos;
            vec4 pos = projection * view * vec4(aPos, 1.0);
            gl_Position = pos.xyww;
        }
        ";

        private static readonly string SkyboxFragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;

        in vec3 TexCoords;

        uniform samplerCube skybox;

        void main()
        {
            FragColor = texture(skybox, TexCoords);
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
            window.Update += Window_Update;
            window.Render += Window_Render;
            window.Closing += Window_Closing;

            window.Run();
        }

        /*private static unsafe void LoadSkybox()
        {
            // Skybox VAO és VBO létrehozása
            float[] skyboxVertices = {
                // positions          
                -1.0f,  1.0f, -1.0f,
                -1.0f, -1.0f, -1.0f,
                 1.0f, -1.0f, -1.0f,
                 1.0f, -1.0f, -1.0f,
                 1.0f,  1.0f, -1.0f,
                -1.0f,  1.0f, -1.0f,

                -1.0f, -1.0f,  1.0f,
                -1.0f, -1.0f, -1.0f,
                -1.0f,  1.0f, -1.0f,
                -1.0f,  1.0f, -1.0f,
                -1.0f,  1.0f,  1.0f,
                -1.0f, -1.0f,  1.0f,

                 1.0f, -1.0f, -1.0f,
                 1.0f, -1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f, -1.0f,
                 1.0f, -1.0f, -1.0f,

                -1.0f, -1.0f,  1.0f,
                -1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f, -1.0f,  1.0f,
                -1.0f, -1.0f,  1.0f,

                -1.0f,  1.0f, -1.0f,
                 1.0f,  1.0f, -1.0f,
                 1.0f,  1.0f,  1.0f,
                 1.0f,  1.0f,  1.0f,
                -1.0f,  1.0f,  1.0f,
                -1.0f,  1.0f, -1.0f,

                -1.0f, -1.0f, -1.0f,
                -1.0f, -1.0f,  1.0f,
                 1.0f, -1.0f, -1.0f,
                 1.0f, -1.0f, -1.0f,
                -1.0f, -1.0f,  1.0f,
                 1.0f, -1.0f,  1.0f
            };

            skyboxVAO = Gl.GenVertexArray();
            skyboxVBO = Gl.GenBuffer();

            Gl.BindVertexArray(skyboxVAO);
            Gl.BindBuffer(BufferTargetARB.ArrayBuffer, skyboxVBO);
            Gl.BufferData(BufferTargetARB.ArrayBuffer, (uint)(skyboxVertices.Length * sizeof(float)), skyboxVertices, BufferUsageARB.StaticDraw);
            Gl.EnableVertexAttribArray(0);
            Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

            uint skyboxVertexShader = Gl.CreateShader(ShaderType.VertexShader);
            Gl.ShaderSource(skyboxVertexShader, SkyboxVertexShaderSource);
            Gl.CompileShader(skyboxVertexShader);

            uint skyboxFragmentShader = Gl.CreateShader(ShaderType.FragmentShader);
            Gl.ShaderSource(skyboxFragmentShader, SkyboxFragmentShaderSource);
            Gl.CompileShader(skyboxFragmentShader);


        }*/

        private static class MathHelper
        {
            public static float DegreesToRadians(float degrees)
            {
                return degrees * (MathF.PI / 180f);
            }
        }

        private static unsafe void SetMaterialColor()
        {
            int location = Gl.GetUniformLocation(program, "uMaterialColor");

            if (location == -1)
            {
                throw new Exception("uMaterialColor uniform not found on shader.");
            }

            Gl.Uniform3(location, 0.3f, 0.3f, 0.3f);
        }

        private static void Window_Load()
        {
            //Console.WriteLine("Load");

            // set up input handling
            Gl = window.CreateOpenGL();

            inputContext = window.CreateInput();

            controller = new ImGuiController(Gl, window, inputContext);

            /*carPosition = new Vector3D<float>(0f, 0f, 0f);
            carDirection = new Vector3D<float>(0f, 0f, 1f);
            carRotation = 0f; // in degrees
            */
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

            materials = MtlLoader.Load("car.mtl");

            if (materials.TryGetValue("rubber", out var rubberMaterial))
            {
                currentMaterial = rubberMaterial;
            }
            if (currentMaterial != null)
            {
                SetMaterialUniforms(currentMaterial);
            }
            else
            {
                Console.WriteLine("No material");
            }

            for (int i = 0; i < 360; i += 10)
            {
                float angle = MathHelper.DegreesToRadians(i);
                roadPoints.Add(new Vector3D<float>(
                    MathF.Sin(angle) * 20f,
                    0f,
                    MathF.Cos(angle) * 20f
                    ));
            }

            Gl.ClearColor(System.Drawing.Color.Black);

            SetUpObjects();

            Gl.Enable(EnableCap.CullFace);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);
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
            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);

            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("Vertex shader failed to compile: " + Gl.GetShaderInfoLog(vshader));

            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader);

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);
            Gl.GetProgram(program, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                Console.WriteLine($"Error linking shader {Gl.GetProgramInfoLog(program)}");
            }
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

                SetViewMatrix(viewMatrix);
                isUserInputDetected = false;
            }
        }

        private static void Window_Update(double deltaTime)
        {
            if (isUserInputDetected)
            {
                UpdateCamera();
                //UpdateCarDirection();
            }

            controller.Update((float)deltaTime);
        }

        private static unsafe void Window_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s].");

            // GL here
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);


            Gl.UseProgram(program);

            Vector3D<float> forward = Vector3D.Normalize(carDirection);
            Vector3D<float> up = new Vector3D<float>(0f, 1f, 0f);
            float distanceBack = 10f;
            float heightUp = 3f;
            Vector3D<float> cameraPosition = carPosition + new Vector3D<float>(0f, heightUp, -distanceBack);
            var viewMatrix = Matrix4X4.CreateLookAt(cameraPosition, carPosition, up);

            SetViewMatrix(viewMatrix);
            SetProjectionMatrix();

            SetLightColor();
            SetLightPosition();
            SetViewerPosition();
            SetShininess();
            SetMaterialColor();

            var roadMatrix = Matrix4X4<float>.Identity;
            SetModelMatrix(roadMatrix);
            Gl.BindVertexArray(road.Vao);
            Gl.DrawElements(GLEnum.Triangles, road.IndexArrayLength, GLEnum.UnsignedInt, null);

            foreach (var building in buildings)
            {
                var buildingMatrix = Matrix4X4<float>.Identity;
                SetModelMatrix(buildingMatrix);
                Gl.BindVertexArray(building.Vao);
                Gl.DrawElements(GLEnum.Triangles, building.IndexArrayLength, GLEnum.UnsignedInt, null);
            }

            DrawCar();

            //ImGuiNET.ImGui.ShowDemoWindow();
            ImGuiNET.ImGui.Begin("Lighting properties",
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
            ImGuiNET.ImGui.SliderFloat("Shininess", ref Shininess, 1, 200);
            ImGuiNET.ImGui.End();


            controller.Render();
        }

        private static unsafe void SetLightColor()
        {
            int location = Gl.GetUniformLocation(program, LightColorVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightColorVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, 1f, 1f, 1f);
            CheckError();
        }

        private static unsafe void SetLightPosition()
        {
            int location = Gl.GetUniformLocation(program, LightPositionVariableName);

            if (location == -1)
            {
                throw new Exception($"{LightPositionVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, 0f, 10f, 0f);
            CheckError();
        }

        private static unsafe void SetViewerPosition()
        {
            int location = Gl.GetUniformLocation(program, ViewPosVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewPosVariableName} uniform not found on shader.");
            }

            Gl.Uniform3(location, cameraDescriptor.Position.X, cameraDescriptor.Position.Y, cameraDescriptor.Position.Z);
            CheckError();
        }

        private static unsafe void SetShininess()
        {
            int location = Gl.GetUniformLocation(program, ShininessVariableName);

            if (location == -1)
            {
                throw new Exception($"{ShininessVariableName} uniform not found on shader.");
            }

            Gl.Uniform1(location, Shininess);
            CheckError();
        }

        private static unsafe void DrawCar()
        {
            // set material uniform to rubber
            var modelMatrix = Matrix4X4<float>.Identity;

            var rotationMatrix = Matrix4X4.CreateRotationY(MathHelper.DegreesToRadians(carRotation));
            var translationMatrix = Matrix4X4.CreateTranslation(carPosition);
            modelMatrix = translationMatrix * rotationMatrix;

            SetModelMatrix(modelMatrix);

            teapot = ObjResourceReader.CreateTeapotWithColor(Gl, face1Color);

            Gl.BindVertexArray(teapot.Vao);
            Gl.DrawElements(GLEnum.Triangles, teapot.IndexArrayLength, GLEnum.UnsignedInt, null);
            Gl.BindVertexArray(0);
        }

        private static unsafe void SetModelMatrix(Matrix4X4<float> modelMatrix)
        {
            int location = Gl.GetUniformLocation(program, ModelMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{ModelMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&modelMatrix);
            CheckError();

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
            CheckError();
        }

        private static unsafe void SetUpObjects()
        {

            float[] face1Color = [1f, 0f, 0f, 1.0f];
            float[] face2Color = [0.0f, 1.0f, 0.0f, 1.0f];
            float[] face3Color = [0.0f, 0.0f, 1.0f, 1.0f];
            float[] face4Color = [1.0f, 0.0f, 1.0f, 1.0f];
            float[] face5Color = [0.0f, 1.0f, 1.0f, 1.0f];
            float[] face6Color = [1.0f, 1.0f, 0.0f, 1.0f];

            //teapot = ObjResourceReader.CreateTeapotWithColor(Gl, face1Color);
            road = CreateRoadMesh();

            for (int i = 0; i < 10; i++)
            {
                buildings.Add(CreateBuilding(Gl,
                    new Vector3D<float>(i * 15f - 30f, 0f, 20f),
                    new Vector3D<float>(5f, 10f, 5f)));
            }
        }

        private static GlObject CreateBuilding(GL gl, Vector3D<float> position, Vector3D<float> size)
        {
            float[] vertices =
            {
                position.X, position.Y, position.Z, 0, 0, 1, 0.8f,0.8f,0.8f,
                position.X+size.X, position.Y, position.Z, 0,0,1, 0.8f,0.8f,0.8f,
                position.X+size.X, position.Y+size.Y, position.Z, 0,0,1, 0.8f,0.8f,0.8f,
                position.X, position.Y+size.Y, position.Z, 0,0,1, 0.8f,0.8f,0.8f,

                // Hátsó lap
                position.X, position.Y, position.Z+size.Z, 0,0,-1, 0.8f,0.8f,0.8f,
                position.X+size.X, position.Y, position.Z+size.Z, 0,0,-1, 0.8f,0.8f,0.8f,
                position.X+size.X, position.Y+size.Y, position.Z+size.Z, 0,0,-1, 0.8f,0.8f,0.8f,
                position.X, position.Y+size.Y, position.Z+size.Z, 0,0,-1, 0.8f,0.8f,0.8f,
        
                // Bal oldal
                position.X, position.Y, position.Z, -1,0,0, 0.7f,0.7f,0.7f,
                position.X, position.Y, position.Z+size.Z, -1,0,0, 0.7f,0.7f,0.7f,
                position.X, position.Y+size.Y, position.Z+size.Z, -1,0,0, 0.7f,0.7f,0.7f,
                position.X, position.Y+size.Y, position.Z, -1,0,0, 0.7f,0.7f,0.7f,
        
                // Jobb oldal
                position.X+size.X, position.Y, position.Z, 1,0,0, 0.7f,0.7f,0.7f,
                position.X+size.X, position.Y, position.Z+size.Z, 1,0,0, 0.7f,0.7f,0.7f,
                position.X+size.X, position.Y+size.Y, position.Z+size.Z, 1,0,0, 0.7f,0.7f,0.7f,
                position.X+size.X, position.Y+size.Y, position.Z, 1,0,0, 0.7f,0.7f,0.7f,
        
                // Teteje
                position.X, position.Y+size.Y, position.Z, 0,1,0, 0.9f,0.9f,0.9f,
                position.X+size.X, position.Y+size.Y, position.Z, 0,1,0, 0.9f,0.9f,0.9f,
                position.X+size.X, position.Y+size.Y, position.Z+size.Z, 0,1,0, 0.9f,0.9f,0.9f,
                position.X, position.Y+size.Y, position.Z+size.Z, 0,1,0, 0.9f,0.9f,0.9f,
        
                // Alja
                position.X, position.Y, position.Z, 0,-1,0, 0.5f,0.5f,0.5f,
                position.X+size.X, position.Y, position.Z, 0,-1,0, 0.5f,0.5f,0.5f,
                position.X+size.X, position.Y, position.Z+size.Z, 0,-1,0, 0.5f,0.5f,0.5f,
                position.X, position.Y, position.Z+size.Z, 0,-1,0, 0.5f,0.5f,0.5f
            };

            uint[] indices =
            {
                0,1,2, 0,2,3,
                4,5,6, 4,6,7,
                8,9,10, 8,10,11,
                12,13,14, 12,14,15,
                16,17,18, 16,18,19,
                20,21,22, 20,22,23
            };

            return GlObject.Create(gl, vertices, indices);
        }

        private static GlObject CreateRoadMesh()
        {
            List<float> vertices = new List<float>();
            List<uint> indices = new List<uint>();

            float roadWidth = 3.0f;

            for (int i = 0; i < roadPoints.Count; i++)
            {
                Vector3D<float> current = roadPoints[i];
                Vector3D<float> next = roadPoints[(i + 1) % roadPoints.Count];
                Vector3D<float> direction = Vector3D.Normalize(next - current);
                Vector3D<float> perpendicular = Vector3D.Normalize(new Vector3D<float>(-direction.Z, 0, direction.X));

                // ut szelei
                Vector3D<float> left = current + perpendicular * roadWidth;
                Vector3D<float> right = current - perpendicular * roadWidth;

                vertices.AddRange(new[] { left.X, left.Y, left.Z, 0, 1, 0, 0.2f, 0.2f, 0.2f });
                vertices.AddRange(new[] { right.X, right.Y, right.Z, 0, 1, 0, 0.2f, 0.2f, 0.2f });
            }

            for (uint i = 0; i < roadPoints.Count - 1; i++)
            {
                indices.AddRange(new[] { i * 2, i * 2 + 1, (i + 1) * 2 });
                indices.AddRange(new[] { i * 2 + 1, (i + 1) * 2, (i + 1) * 2 + 1 });
            }

            return GlObject.Create(Gl, vertices.ToArray(), indices.ToArray());
        }
        private static void Window_Closing()
        {
            teapot.ReleaseGlObject();
        }

        private static unsafe void SetProjectionMatrix()
        {
            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)Math.PI / 4f, 1024f / 768f, 0.1f, 100);
            int location = Gl.GetUniformLocation(program, ProjectionMatrixVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewMatrixVariableName} uniform not found on shader.");
            }

            Gl.UniformMatrix4(location, 1, false, (float*)&projectionMatrix);
            CheckError();
        }

        private static unsafe void SetViewMatrix(Matrix4X4<float> viewMatrix)
        {
            Gl.UseProgram(program);

            int viewMatrixLocation = Gl.GetUniformLocation(program, ViewMatrixVariableName);

            Gl.UniformMatrix4(viewMatrixLocation, 1, false, (float*)&viewMatrix);
        }

        public static void CheckError()
        {
            var error = (ErrorCode)Gl.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
        }
    }
}