using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;

class Program
{
    private static IWindow _window = null!;
    private static GL _gl = null!;

    private static uint _shaderProgram;

    private static uint _vaoFlat, _vboFlat;
    private static uint _vaoTilted, _vboTilted;

    const int NumPanels = 18;
    const float PanelWidth = 1.0f;
    const float PanelHeight = 2.0f;

    private static uint CreateShaderProgram()
    {
        string vertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec3 aNormal;

            uniform mat4 model;
            uniform mat4 view;
            uniform mat4 projection;

            out vec3 Normal;
            out vec3 FragPos;

            void main()
            {
                FragPos = vec3(model * vec4(aPosition, 1.0));
                Normal = mat3(transpose(inverse(model))) * aNormal;
                gl_Position = projection * view * vec4(FragPos, 1.0);
            }
        ";

        string fragmentShaderSource = @"
            #version 330 core
            in vec3 Normal;
            in vec3 FragPos;

            out vec4 FragColor;

            uniform vec3 lightPos;
            uniform vec3 viewPos;

            void main()
            {
                // Simple diffuse lighting
                vec3 lightDir = normalize(lightPos - FragPos);
                float diff = max(dot(Normal, lightDir), 0.0);
                vec3 diffuse = diff * vec3(1.0, 1.0, 1.0);

                FragColor = vec4(diffuse, 1.0);
            }
        ";

        uint vertexShader = _gl.CreateShader(GLEnum.VertexShader);
        _gl.ShaderSource(vertexShader, vertexShaderSource);
        _gl.CompileShader(vertexShader);

        uint fragmentShader = _gl.CreateShader(GLEnum.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        uint shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(shaderProgram, vertexShader);
        _gl.AttachShader(shaderProgram, fragmentShader);
        _gl.LinkProgram(shaderProgram);

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        return shaderProgram;
    }

    private static void SetupVaoAndVbo(float[] vertices, out uint vao, out uint vbo)
    {
        vao = _gl.GenVertexArray();
        vbo = _gl.GenBuffer();

        _gl.BindVertexArray(vao);
        _gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
        _gl.BufferData<float>(GLEnum.ArrayBuffer, vertices, GLEnum.StaticDraw);

        _gl.VertexAttribPointer(0, 3, GLEnum.Float, false, 6 * sizeof(float), 0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 3, GLEnum.Float, false, 6 * sizeof(float), 3 * sizeof(float));
        _gl.EnableVertexAttribArray(1);

        _gl.BindVertexArray(0);
    }

    public static void Main()
    {
        var options = WindowOptions.Default;
        options.Size = new Silk.NET.Maths.Vector2D<int>(800, 600);
        options.Title = "Lab3-1";

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;

        _window.Run();
    }

    private static void OnLoad()
    {
        _gl = GL.GetApi(_window);

        _gl.Enable(GLEnum.DepthTest);
        _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);

        _shaderProgram = CreateShaderProgram();

        float[] flatVertices = {
            -PanelWidth / 2, -PanelHeight / 2, 0,  0, 0, 1,
             PanelWidth / 2, -PanelHeight / 2, 0,  0, 0, 1,
             PanelWidth / 2,  PanelHeight / 2, 0,  0, 0, 1,

            -PanelWidth / 2, -PanelHeight / 2, 0,  0, 0, 1,
             PanelWidth / 2,  PanelHeight / 2, 0,  0, 0, 1,
            -PanelWidth / 2,  PanelHeight / 2, 0,  0, 0, 1,
        };

        SetupVaoAndVbo(flatVertices, out _vaoFlat, out _vboFlat);

        float tiltRad = MathF.PI / 18f; // 10 fok
        float nx = MathF.Sin(tiltRad);
        float nz = MathF.Cos(tiltRad);

        float[] tiltedVertices = {
            -PanelWidth / 2, -PanelHeight / 2, 0,  nx, 0, nz,
             PanelWidth / 2, -PanelHeight / 2, 0,  nx, 0, nz,
             PanelWidth / 2,  PanelHeight / 2, 0,  nx, 0, nz,

            -PanelWidth / 2, -PanelHeight / 2, 0,  nx, 0, nz,
             PanelWidth / 2,  PanelHeight / 2, 0,  nx, 0, nz,
            -PanelWidth / 2,  PanelHeight / 2, 0,  nx, 0, nz,
        };

        SetupVaoAndVbo(tiltedVertices, out _vaoTilted, out _vboTilted);
    }

    private static void OnRender(double obj)
    {
        var lightPos = new Vector3(0f, 0f, 5f); // kamera felol jojjon a feny
        var viewPos = new Vector3(0f, 0f, 10f);

        int lightPosLoc = _gl.GetUniformLocation(_shaderProgram, "lightPos");
        int viewPosLoc = _gl.GetUniformLocation(_shaderProgram, "viewPos");

        _gl.Uniform3(lightPosLoc, lightPos.X, lightPos.Y, lightPos.Z);
        _gl.Uniform3(viewPosLoc, viewPos.X, viewPos.Y, viewPos.Z);


        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        _gl.UseProgram(_shaderProgram);

        var view = Matrix4x4.CreateTranslation(0.0f, 0.0f, -10.0f);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 4.0f, 800f / 600f, 0.1f, 100.0f);

        SetMatrix4(_gl, _gl.GetUniformLocation(_shaderProgram, "view"), view);
        SetMatrix4(_gl, _gl.GetUniformLocation(_shaderProgram, "projection"), projection);

        DrawBarrel(false, 1.5f);
        DrawBarrel(true, -1.5f);
    }

    private static void DrawBarrel(bool tiltedNormals, float yOffset)
    {
        _gl.BindVertexArray(tiltedNormals ? _vaoTilted : _vaoFlat);

        float angleStep = 360f / NumPanels;
        float angleRad = MathF.PI * angleStep / 180f;
        float radius = PanelWidth / (2 * MathF.Tan(angleRad / 2f));

        for (int i = 0; i < NumPanels; i++)
        {
            float angle = i * angleStep;

            var model = Matrix4x4.CreateTranslation(0, 0, radius) *
                        Matrix4x4.CreateRotationY(MathF.PI * angle / 180f) *
                        Matrix4x4.CreateTranslation(0, yOffset, 0);

            SetMatrix4(_gl, _gl.GetUniformLocation(_shaderProgram, "model"), model);
            _gl.DrawArrays(GLEnum.Triangles, 0, 6);
        }

        _gl.BindVertexArray(0);
    }

    private static void SetMatrix4(GL gl, int location, Matrix4x4 matrix)
    {
        float[] matrixData = new float[16]
        {
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44
        };

        gl.UniformMatrix4(location, 1, false, matrixData);
    }
}