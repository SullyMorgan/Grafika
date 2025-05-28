using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System.Numerics;
using StbImageSharp;
using Silk.NET.Core.Attributes;

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
        private static GlObject? road;
        private static uint roadVAO, roadVBO;

        private static Vector3D<float> carPosition = new Vector3D<float>(0f, 0f, 0f);
        private static Vector3D<float> carDirection = new Vector3D<float>(0f, 0f, 1f);
        private static float carRotation = 0f; // in radians

        private static Matrix4X4<float> viewMatrix;

        private static Vector3D<float> currentCameraPosition;

        private static float Shininess = 50;

        private const string ModelMatrixVariableName = "model";
        private const string NormalMatrixVariableName = "normal";
        private const string ViewMatrixVariableName = "view";
        private const string ProjectionMatrixVariableName = "projection";

        private static bool isUserInputDetected = false;

        // skybox
        private static readonly float[] SkyboxVertices =
        {
            -1000.0f,  1000.0f, -1000.0f,
            -1000.0f, -1000.0f, -1000.0f,
             1000.0f, -1000.0f, -1000.0f,
             1000.0f,  1000.0f, -1000.0f,
            -1000.0f, -1000.0f,  1000.0f,
            -1000.0f,  1000.0f,  1000.0f,
             1000.0f, -1000.0f,  1000.0f,
             1000.0f,  1000.0f,  1000.0f
        };

        private static readonly uint[] SkyboxIndices = {
            0, 1, 2, 2, 3, 0,
            4, 1, 0, 0, 5, 4,
            2, 6, 7, 7, 3, 2,
            4, 5, 7, 7, 6, 4,
            0, 3, 7, 7, 5, 0,
            1, 4, 6, 6, 2, 1
        };

        private static uint skyboxVAO, skyboxVBO, skyboxEBO;
        private static uint skyboxProgram;
        private static uint cubemapTexture;
        private static Matrix4X4<float> projectionMatrix;

        // kontrolok
        private static bool isWPressed = false;
        private static bool isSPressed = false;
        private static bool isAPressed = false;
        private static bool isDPressed = false;

        private static float continuousMoveSpeed = 30.0f;
        private static float continuousRotationSpeedDegrees = 90.0f;

        // uj ut
        private static List<Vector3D<float>> roadCenterline = new List<Vector3D<float>>();
        private static float roadWidth = 30.0f;
        private static uint roadIndicesCount = 0;
        private static float roadSurfaceYOffset = -1f;
        private static float offRoadTolerance = 1.0f;

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

        private static readonly string SkyboxVertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPos;

        uniform mat4 projection;
        uniform mat4 view;

        out vec3 TexCoords;

        void main()
        {
            TexCoords = aPos;
            mat4 viewWithoutTranslation = mat4(mat3(view)); // Pozíció eltávolítása
            vec4 pos = projection * viewWithoutTranslation * vec4(aPos, 1.0);
            gl_Position = pos.xyww; // Z-t mindig 1.0-re állítja
        }
        ";

        private static readonly string SkyboxFragmentShaderSource = @"
        #version 330 core
        in vec3 TexCoords;
        out vec4 FragColor;

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
            //Console.WriteLine("Initializing OpenGL...");

            Gl = window.CreateOpenGL();
            if (Gl == null)
            {
                Console.WriteLine("Failed to initialize OpenGL");
            }
            else
            {
                //Console.WriteLine("OpenGL initialized successfully");
            }
            inputContext = window.CreateInput();
            if (inputContext == null)
            {
                Console.WriteLine("Failed to create input context");
            }
            else
            {
                //Console.WriteLine("Input contex created.");
            }
            CheckError("ha a loadban is van error beleverem a faszomat");
            controller = new ImGuiController(Gl, window, inputContext);

            LinkProgram();
            Gl.UseProgram(program);
            foreach (var keyboard in inputContext.Keyboards)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
                keyboard.KeyUp += Keyboard_KeyUp;
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

            if (car == null)
            {
                throw new Exception("Failed to create car object from OBJ file.");
            }

            roadCenterline.Add(new Vector3D<float>(0, 0, -150));     // Start messzebb hátul

            // Első egyenes szakasz
            roadCenterline.Add(new Vector3D<float>(0, 0, 50));       // Hosszú egyenes előre

            // Első kanyar (enyhe jobbos)
            roadCenterline.Add(new Vector3D<float>(20, 0, 100));
            roadCenterline.Add(new Vector3D<float>(50, 0, 130));
            roadCenterline.Add(new Vector3D<float>(90, 0, 150));

            // Második egyenes szakasz
            roadCenterline.Add(new Vector3D<float>(200, 0, 150));    // Hosszú egyenes oldalra

            // S kanyar
            roadCenterline.Add(new Vector3D<float>(250, 0, 130));    // Jobbra le
            roadCenterline.Add(new Vector3D<float>(280, 0, 100));
            roadCenterline.Add(new Vector3D<float>(280, 0, 50));     // Egyenes lefelé
            roadCenterline.Add(new Vector3D<float>(250, 0, 20));     // Balra fel
            roadCenterline.Add(new Vector3D<float>(200, 0, 0));

            // Harmadik egyenes szakasz visszafelé
            roadCenterline.Add(new Vector3D<float>(100, 0, 0));
            roadCenterline.Add(new Vector3D<float>(50, 0, -20));     // Enyhe kanyar

            // Utolsó szakasz a cél felé
            roadCenterline.Add(new Vector3D<float>(50, 0, -180));    // Hosszú egyenes a célhoz
            roadCenterline.Add(new Vector3D<float>(40, 0, -220));

            if (roadCenterline.Count > 0)
            {
                carPosition = roadCenterline[0];
                if (roadCenterline.Count > 1)
                {
                    carDirection = Vector3D.Normalize(roadCenterline[1] - roadCenterline[0]);
                    carRotation = MathF.Atan2(carDirection.X, carDirection.Z);
                }
            }
            CreateRoad(Gl);

            CreateSkybox(Gl);
            CreateSkyboxShader();

            string[] faces = {
            "right.jpg",
            "left.jpg",
            "top.jpg",
            "bottom.jpg",
            "front.jpg",
            "back.jpg"
            };

            cubemapTexture = LoadCubemap(faces);

            UpdateCamera();
            SetViewMatrix(viewMatrix);

            Gl.ClearColor(System.Drawing.Color.White);
            Gl.Enable(EnableCap.DepthTest);
            Gl.FrontFace(GLEnum.CW);

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

        private static unsafe uint LoadCubemap(string[] faces)
        {
            uint textureID = Gl.GenTexture();
            Gl.BindTexture(GLEnum.TextureCubeMap, textureID);

            int skyboxLoc = Gl.GetUniformLocation(skyboxProgram, "skybox");
            if (skyboxLoc == -1)
            {
                throw new Exception("skybox uniform not found on skybox shader.");
            }

            GLEnum[] targets = new GLEnum[]
            {
                GLEnum.TextureCubeMapPositiveX,
                GLEnum.TextureCubeMapNegativeX,
                GLEnum.TextureCubeMapPositiveY,
                GLEnum.TextureCubeMapNegativeY,
                GLEnum.TextureCubeMapPositiveZ,
                GLEnum.TextureCubeMapNegativeZ
            };

            for (uint i = 0; i < faces.Length; i++)
            {
                string filePath = Path.Combine("Resources", "skybox", faces[i]);
                var img = ImageResult.FromStream(File.OpenRead(filePath), ColorComponents.RedGreenBlueAlpha);

                fixed (byte* data = img.Data)
                {
                    Gl.TexImage2D(
                        targets[i],
                        0,
                        (int)GLEnum.Rgba,
                        (uint)img.Width,
                        (uint)img.Height,
                        0,
                        GLEnum.Rgba,
                        GLEnum.UnsignedByte,
                        data
                        );
                }
            }

            Gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            Gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            Gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            Gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
            Gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);

            return textureID;
        }

        private static unsafe void CreateSkybox(GL gl)
        {
            skyboxVAO = gl.GenVertexArray();
            skyboxVBO = gl.GenBuffer();
            skyboxEBO = gl.GenBuffer();

            gl.BindVertexArray(skyboxVAO);

            gl.BindBuffer(GLEnum.ArrayBuffer, skyboxVBO);
            fixed (float* v = SkyboxVertices)
            {
                gl.BufferData(
                    GLEnum.ArrayBuffer,
                    (nuint)(SkyboxVertices.Length * sizeof(float)),
                    v,
                    GLEnum.StaticDraw
                    );
            }

            gl.BindBuffer(GLEnum.ElementArrayBuffer, skyboxEBO);
            fixed (uint* i = SkyboxIndices)
            {
                gl.BufferData(
                    GLEnum.ElementArrayBuffer,
                    (nuint)(SkyboxIndices.Length * sizeof(uint)),
                    i,
                    GLEnum.StaticDraw
                    );
            }

            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, 3 * sizeof(float), (void*)0);
            gl.EnableVertexAttribArray(0);
            gl.BindVertexArray(0);
        }

        private static void CreateSkyboxShader()
        {
            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            Gl.ShaderSource(vshader, SkyboxVertexShaderSource);
            Gl.CompileShader(vshader);
            CheckShaderError(vshader);

            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);
            Gl.ShaderSource(fshader, SkyboxFragmentShaderSource);
            Gl.CompileShader(fshader);
            CheckShaderError(fshader);

            skyboxProgram = Gl.CreateProgram();
            Gl.AttachShader(skyboxProgram, vshader);
            Gl.AttachShader(skyboxProgram, fshader);
            Gl.LinkProgram(skyboxProgram);
            CheckProgramLink(skyboxProgram);

            Gl.DetachShader(skyboxProgram, vshader);
            Gl.DetachShader(skyboxProgram, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);
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
            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);
        }

        private static void Keyboard_KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            isUserInputDetected = true;

            switch (key)
            {
                case Key.W:
                    isWPressed = true;
                    break;
                case Key.S:
                    isSPressed = true;
                    break;
                case Key.A:
                    isAPressed = true;
                    break;
                case Key.D:
                    isDPressed = true;
                    break;
            }
        }

        private static void Keyboard_KeyUp(IKeyboard keyboard, Key key, int arg3)
        {
            isUserInputDetected = false;

            switch (key)
            {
                case Key.W:
                    isWPressed = false;
                    break;
                case Key.S:
                    isSPressed = false;
                    break;
                case Key.A:
                    isAPressed = false;
                    break;
                case Key.D:
                    isDPressed = false;
                    break;
            }
        }

        private static void UpdateCarDirection()
        {
            carDirection = new Vector3D<float>(
                MathF.Sin(carRotation),
                0f,
                MathF.Cos(carRotation)
                );
            //Console.WriteLine($"CARROTATION: {carRotation}, CARDIRECTION: {carDirection}");

            carDirection = Vector3D.Normalize(carDirection);
        }

        private static void UpdateCamera()
        {
            Vector3D<float> cameraOffset = new Vector3D<float>(0f, 5f, 15f);

            Vector3D<float> calculatedCameraPosition = carPosition - carDirection * cameraOffset.Z + new Vector3D<float>(0f, cameraOffset.Y, 0f);

            currentCameraPosition = calculatedCameraPosition;

            Vector3D<float> cameraTarget = carPosition;
            Vector3D<float> up = new Vector3D<float>(0f, 1f, 0f);

            viewMatrix = Matrix4X4.CreateLookAt(calculatedCameraPosition, cameraTarget, up);
            SetViewMatrix(viewMatrix);
        }


        private static void Window_Update(double deltaTime)
        {
            bool carStateChanged = false;
            float dt = (float)deltaTime;

            if (isAPressed)
            {
                carRotation += MathHelper.DegreesToRadians(continuousRotationSpeedDegrees * dt);
                carStateChanged = true;
            }
            if (isDPressed)
            {
                carRotation -= MathHelper.DegreesToRadians(continuousRotationSpeedDegrees * dt);
                carStateChanged = true;
            }

            if (isAPressed || isDPressed)
            {
                UpdateCarDirection();
            }

            if (isWPressed)
            {
                carPosition += carDirection * continuousMoveSpeed * dt;
                carStateChanged = true;
            }

            if (isSPressed)
            {
                carPosition -= carDirection * continuousMoveSpeed * dt;
                carStateChanged = true;
            }

            if (carStateChanged)
            {
                UpdateCamera();

                if (IsCarOffRoad())
                {
                    Console.WriteLine("Vesztettel! Leestel az utrol!");
                    window.Close();
                    return;
                }
            }

            if (roadCenterline.Count > 0)
            {
                Vector3D<float> targetPoint = roadCenterline[roadCenterline.Count - 1];
                float distanceToTarget = Vector3D.Distance(carPosition, targetPoint);
                if (distanceToTarget < 5.0f)
                {
                    Console.WriteLine("Gratulalok! Nyertel!");
                    window.Close();
                }
            }
        }

        private static unsafe void Window_Render(double deltaTime)
        {
            //Console.WriteLine($"Render after {deltaTime} [s].");

            // GL here
            while (Gl.GetError() != GLEnum.NoError) { }
            controller.Update((float)deltaTime);
            Gl.Clear(ClearBufferMask.ColorBufferBit);
            Gl.Clear(ClearBufferMask.DepthBufferBit);

            CheckError("Checkerror a renderben");
            Gl.UseProgram(program);
             
            if (program == 0)
            {
                Console.WriteLine("Shader program is 0! Ez baj lesz");
            }

            UpdateCamera();

            SetProjectionMatrix();
            SetLightColor();
            SetLightPosition();
            SetViewerPosition();
            SetShininess();

            DrawCar();
            
            Gl.UseProgram(program);
            SetModelMatrix(Matrix4X4<float>.Identity);
            Gl.BindVertexArray(roadVAO);
            Gl.DrawElements(GLEnum.Triangles, roadIndicesCount, GLEnum.UnsignedInt, (void*)0);
            Gl.BindVertexArray(0);

            Gl.DepthFunc(GLEnum.Lequal); // Módosítjuk a mélységtesztet

            Gl.UseProgram(skyboxProgram);
            var viewWithoutTranslation = new Matrix4X4<float>(
                viewMatrix.M11, viewMatrix.M12, viewMatrix.M13, 0,
                viewMatrix.M21, viewMatrix.M22, viewMatrix.M23, 0,
                viewMatrix.M31, viewMatrix.M32, viewMatrix.M33, 0,
                0, 0, 0, 1
            );
            
            SetShaderMatrix(skyboxProgram, "view", viewWithoutTranslation);
            float fov = MathF.PI / 4f;
            float aspectRatio = (float)window.Size.X / window.Size.Y;
            float near = 0.1f;
            float far = 2000.0f;
            var localProjectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView(fov, aspectRatio, near, far);

            SetShaderMatrix(skyboxProgram, "projection", localProjectionMatrix);

            Gl.BindVertexArray(skyboxVAO);
            Gl.ActiveTexture(GLEnum.Texture0);
            Gl.BindTexture(GLEnum.TextureCubeMap, cubemapTexture);
            Gl.Uniform1(Gl.GetUniformLocation(skyboxProgram, "skybox"), 0);
            Gl.DrawElements(GLEnum.Triangles, 36, GLEnum.UnsignedInt, (void*)0);
            Gl.BindVertexArray(0);

            CheckError("Skybox csinalasa utan a renderben");

            Gl.DepthFunc(GLEnum.Less);

            //ImGuiNET.ImGui.ShowDemoWindow();
            ImGuiNET.ImGui.Begin("Lighting properties",
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
            ImGuiNET.ImGui.SliderFloat("Shininess", ref Shininess, 1, 200);
            ImGuiNET.ImGui.End();

            controller.Render();
            //Console.WriteLine("ImGui rendered.");
        }


        private static unsafe void SetLightColor()
        {
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
            int location = Gl.GetUniformLocation(program, ViewPosVariableName);

            if (location == -1)
            {
                throw new Exception($"{ViewPosVariableName} uniform not found on shader.");
            }

            // Használd a currentCameraPosition változót a viewPos uniformhoz
            Gl.Uniform3(location, currentCameraPosition.X, currentCameraPosition.Y, currentCameraPosition.Z);
            //Console.WriteLine($"SetViewerPosition called with: {currentCameraPosition}");
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
            //var baseRotation = Matrix4X4.CreateRotationY(MathF.PI);
            var currentRotation = Matrix4X4.CreateRotationY(carRotation);
            var translation = Matrix4X4.CreateTranslation(carPosition);
            //var modelMatrix = translation * currentRotation;
            var modelMatrix = currentRotation * translation;

            SetModelMatrix(modelMatrix);
            car.DrawWithMaterials();
        }

        private static unsafe void CreateRoad(GL gl)
        {
            List<float> vertices = new List<float>();
            List<uint> indices = new List<uint>();
            uint vertexCounter = 0;

            Vector3D<float> finishLineColor = new Vector3D<float>(1f, 1f, 1f);
            Vector3D<float> roadColor = new Vector3D<float>(0.3f, 0.3f, 0.3f);
            Vector3D<float> roadNormal = new Vector3D<float>(0f, 1f, 0f);

            Vector3D<float> p_start = roadCenterline[0];
            Vector3D<float> p_next_for_start_dir = roadCenterline[1];
            Vector3D<float> start_direction = Vector3D.Normalize(p_next_for_start_dir - p_start);
            Vector3D<float> start_perp_right = Vector3D.Normalize(new Vector3D<float>(start_direction.Z, 0, -start_direction.X));

            Vector3D<float> p0_left_xz = p_start - start_perp_right * (roadWidth / 2.0f);
            Vector3D<float> p0_right_xz = p_start + start_perp_right * (roadWidth / 2.0f);

            vertices.AddRange(new float[] { p0_left_xz.X, p_start.Y + roadSurfaceYOffset, p0_left_xz.Z, roadNormal.X, roadNormal.Y, roadNormal.Z, roadColor.X, roadColor.Y, roadColor.Z });
            vertices.AddRange(new float[] { p0_right_xz.X, p_start.Y + roadSurfaceYOffset, p0_right_xz.Z, roadNormal.X, roadNormal.Y, roadNormal.Z, roadColor.X, roadColor.Y, roadColor.Z });
            vertexCounter += 2;

            // szegmensek letrehozasa
            for (int i = 0; i < roadCenterline.Count - 1; i++)
            {
                Vector3D<float> p1 = roadCenterline[i]; // p1 Y szintje adja az aktuális szegmens alap Y-ját
                Vector3D<float> p2_centerline = roadCenterline[i + 1]; // p2 Y szintje adja a következő pont alap Y-ját

                Vector3D<float> direction = Vector3D.Normalize(p2_centerline - roadCenterline[i]);
                if (direction.LengthSquared < 0.001f) continue;

                Vector3D<float> perpendicularRight = Vector3D.Normalize(new Vector3D<float>(direction.Z, 0, -direction.X));

                Vector3D<float> p2_left_xz = p2_centerline - perpendicularRight * (roadWidth / 2.0f);
                Vector3D<float> p2_right_xz = p2_centerline + perpendicularRight * (roadWidth / 2.0f);

                Vector3D<float> currentSegmentColor;
                if (i == roadCenterline.Count - 2)
                {
                    currentSegmentColor = finishLineColor;
                }
                else
                {
                    currentSegmentColor = roadColor;
                }

                vertices.AddRange(new float[] { p2_left_xz.X, p2_centerline.Y + roadSurfaceYOffset, p2_left_xz.Z, roadNormal.X, roadNormal.Y, roadNormal.Z, currentSegmentColor.X, currentSegmentColor.Y, currentSegmentColor.Z });
                vertices.AddRange(new float[] { p2_right_xz.X, p2_centerline.Y + roadSurfaceYOffset, p2_right_xz.Z, roadNormal.X, roadNormal.Y, roadNormal.Z, currentSegmentColor.X, currentSegmentColor.Y, currentSegmentColor.Z });

                indices.Add(vertexCounter - 2);
                indices.Add(vertexCounter - 1);
                indices.Add(vertexCounter);

                indices.Add(vertexCounter - 1);
                indices.Add(vertexCounter + 1);
                indices.Add(vertexCounter);

                vertexCounter += 2;
            }

            roadVAO = gl.GenVertexArray();
            roadVBO = gl.GenBuffer();
            uint roadEBO = gl.GenBuffer();

            gl.BindVertexArray(roadVAO);

            float[] roadVerticesArray = vertices.ToArray();
            uint[] roadIndicesArray = indices.ToArray();
            roadIndicesCount = (uint)roadIndicesArray.Length;

            gl.BindBuffer(GLEnum.ArrayBuffer, roadVBO);
            fixed (float* roadVerticesPtr = roadVerticesArray)
            {
                gl.BufferData(
                    GLEnum.ArrayBuffer,
                    (nuint)(roadVerticesArray.Length * sizeof(float)),
                    roadVerticesPtr,
                    GLEnum.StaticDraw
                );
            }

            gl.BindBuffer(GLEnum.ElementArrayBuffer, roadEBO);
            fixed (uint* roadIndicesPtr = roadIndicesArray)
            {
                gl.BufferData(
                    GLEnum.ElementArrayBuffer,
                    (nuint)(roadIndicesArray.Length * sizeof(uint)),
                    roadIndicesPtr,
                    GLEnum.StaticDraw
                );
            }

            int stride = 9 * sizeof(float); // 3 pos, 3 normal, 3 color
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)stride, (void*)0); // Position
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)stride, (void*)(3 * sizeof(float))); // Normal
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(2, 3, GLEnum.Float, false, (uint)stride, (void*)(6 * sizeof(float))); // Color
            gl.EnableVertexAttribArray(2);

            gl.BindVertexArray(0);
        }

        private static bool IsCarOffRoad()
        {
            Vector2D<float> carPosXZ = new Vector2D<float>(carPosition.X, carPosition.Z);
            float minDistanceToCenterLineSq = float.MaxValue;

            for (int i = 0; i < roadCenterline.Count - 1; i++)
            {
                Vector3D<float> p1_3D = roadCenterline[i];
                Vector3D<float> p2_3D = roadCenterline[i + 1];

                Vector2D<float> p1 = new Vector2D<float>(p1_3D.X, p1_3D.Z);
                Vector2D<float> p2 = new Vector2D<float>(p2_3D.X, p2_3D.Z);

                Vector2D<float> segmentVec = p2 - p1;
                Vector2D<float> carToP1 = carPosXZ - p1;

                float segmentLengthSq = segmentVec.LengthSquared;

                if (segmentLengthSq == 0)
                {
                    float distSq = carToP1.LengthSquared;
                    if (distSq < minDistanceToCenterLineSq)
                    {
                        minDistanceToCenterLineSq = distSq;
                    }
                    continue;
                }

                float t = Vector2D.Dot(carToP1, segmentVec) / segmentLengthSq;

                Vector2D<float> closestPointOnLine;
                if (t < 0.0f)
                {
                    closestPointOnLine = p1;
                }
                else if (t > 1.0f)
                {
                    closestPointOnLine = p2;
                }
                else
                {
                    closestPointOnLine = p1 + t * segmentVec;
                }

                float distanceToClosestPointSq = Vector2D.DistanceSquared(carPosXZ, closestPointOnLine);
                if (distanceToClosestPointSq < minDistanceToCenterLineSq)
                {
                    minDistanceToCenterLineSq = distanceToClosestPointSq;
                }
            }

            float minDistance = MathF.Sqrt(minDistanceToCenterLineSq);
            float maxAllowedDistance = (roadWidth / 2.0f) + offRoadTolerance;

            return minDistance > maxAllowedDistance;
        }

        private static unsafe void SetModelMatrix(Matrix4X4<float> modelMatrix)
        {
            Gl.UseProgram(program);
            int location = Gl.GetUniformLocation(program, ModelMatrixVariableName);
            if (location == -1)
            {
                throw new Exception($"{ModelMatrixVariableName} uniform not found on shader.");
            }

            //Console.WriteLine($"Uniform location: {location}");
            //Console.WriteLine($"Model matrix: {modelMatrix}");

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

        private static unsafe void SetShaderMatrix(uint program, string name, Matrix4X4<float> matrix)
        {
            int location = Gl.GetUniformLocation(program, name);
            if (location != -1)
            {
                Gl.UniformMatrix4(location, 1, false, (float*)&matrix);
            }
        }

        private static void Window_Closing()
        {
            car.ReleaseGlObject();
            Gl.DeleteVertexArray(skyboxVAO);
            Gl.DeleteBuffer(skyboxVBO);
            Gl.DeleteBuffer(skyboxEBO);
            Gl.DeleteProgram(skyboxProgram);
            Gl.DeleteTexture(cubemapTexture);
        }

        private static unsafe void SetProjectionMatrix()
        {
            if (window.Size.X == 0 || window.Size.Y == 0)
            {
                throw new Exception("Window size is invalid.");
            }
            float aspectRatio = (float)window.Size.X / (float)window.Size.Y;
            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)Math.PI / 4f, aspectRatio, 0.1f, 2000);
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
            Gl.UseProgram(program);
            CheckError("SetViewMatrix eleje");
            int viewMatrixLocation = Gl.GetUniformLocation(program, ViewMatrixVariableName);

            if (viewMatrixLocation == -1)
            {
                throw new Exception($"Uniform {ViewMatrixVariableName} not found on the shader.");
            }

            Gl.UniformMatrix4(viewMatrixLocation, 1, false, (float*)&viewMatrix);
            //Console.WriteLine($"View matrix: {viewMatrix}");
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

        private static void CheckShaderError(uint shader)
        {
            Gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status != (int)GLEnum.True)
            {
                string infoLog = Gl.GetShaderInfoLog(shader);
                throw new Exception($"Shader compilation failed: {infoLog}");
            }
        }

        private static void CheckProgramLink(uint program)
        {
            Gl.GetProgram(program, GLEnum.LinkStatus, out int status);
            if (status == 0)
            {
                string infoLog = Gl.GetProgramInfoLog(program);
                throw new Exception($"Shader program linking failed: {infoLog}");
            }
        }
    }
}