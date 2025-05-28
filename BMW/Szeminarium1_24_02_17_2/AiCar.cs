using Silk.NET.Maths;
using System.Collections.Generic;
using System;

namespace Szeminarium1_24_02_17_2
{
    internal class AiCar
    {
        public GlObject Model { get; set; }
        public Vector3D<float> Position { get; set; }
        public Vector3D<float> Direction { get; set; }
        public float Rotation { get; set; }
        public float Speed { get; set; }

        public int CurrentPathIndex { get; set; }
        public List<Vector3D<float>> Path { get; private set; }

        public AiCar(GlObject model, List<Vector3D<float>> path, float speed = 3.0f)
        {
            Model = model;
            Path = path;
            Speed = speed;
            CurrentPathIndex = 0;

            if (Path != null && Path.Count > 0)
            {
                Position = Path[0];
                if (Path.Count > 1)
                {
                    Direction = Vector3D.Normalize(Path[1] - Path[0]);
                    Rotation = MathF.Atan2(Direction.X, Direction.Z);
                }
                else
                {
                    Direction = new Vector3D<float>(0, 0, 1);
                    Rotation = 0;
                }
            }
        }

        public void Update(float deltaTime)
        {
            if (Path == null || Path.Count < 2 || CurrentPathIndex >= Path.Count - 1)
                return;

            Vector3D<float> targetPoint = Path[CurrentPathIndex + 1];
            Vector3D<float> directionToTarget = Vector3D.Normalize(targetPoint - Position);

            Position += directionToTarget * Speed * deltaTime;
            Direction = directionToTarget;
            Rotation = MathF.Atan2(Direction.X, Direction.Z);

            if (Vector3D.DistanceSquared(Position, targetPoint) < 1.0f)
            {
                CurrentPathIndex++;
                if (CurrentPathIndex >= Path.Count - 1)
                {
                    Console.WriteLine("AI car reached the end.");
                }
            }
        }

        public Matrix4X4<float> GetModelMatrix()
        {
            var RotationMatrix = Matrix4X4.CreateRotationY(Rotation);
            var translationMatrix = Matrix4X4.CreateTranslation(Position);
            Matrix4X4<float> modelMatrix = translationMatrix * RotationMatrix;
            return modelMatrix;
        }

    }
}
