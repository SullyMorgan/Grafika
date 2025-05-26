using Silk.NET.Maths;

namespace Szeminarium1_24_02_17_2
{
    internal class CameraDescriptor
    {
        private double DistanceToTarget = 4;

        private double AngleToZYPlane = 0;

        private double AngleToZXPlane = 0;

        private const double DistanceScaleFactor = 1.1;

        private const double AngleChangeStepSize = Math.PI / 180 * 5;

        public Vector3D<float> Target { get; set; } = Vector3D<float>.Zero;

        public Vector3D<float> Position
        {
            get
            {
                return GetPointFromAngles(DistanceToTarget, AngleToZYPlane, AngleToZXPlane);
            }
        }

        public Vector3D<float> UpVector
        {
            get
            {
                return Vector3D.Normalize(GetPointFromAngles(DistanceToTarget, AngleToZYPlane, AngleToZXPlane + Math.PI / 2));
            }
        }

        public void IncreaseZXAngle()
        {
            Console.WriteLine($"Before IncreaseZXAngle: {AngleToZXPlane}");
            AngleToZXPlane += AngleChangeStepSize;
            Console.WriteLine($"After IncreaseZXAngle: {AngleToZXPlane}");
        }

        public void DecreaseZXAngle()
        {
            AngleToZXPlane -= AngleChangeStepSize;
        }

        public void IncreaseZYAngle()
        {
            AngleToZYPlane += AngleChangeStepSize;

        }

        public void DecreaseZYAngle()
        {
            AngleToZYPlane -= AngleChangeStepSize;
        }

        public void IncreaseDistance()
        {
            DistanceToTarget = DistanceToTarget * DistanceScaleFactor;
        }

        public void DecreaseDistance()
        {
            DistanceToTarget = DistanceToTarget / DistanceScaleFactor;
        }
        
        private static Vector3D<float> GetPointFromAngles(double DistanceToTarget, double angleToMinZYPlane, double angleToMinZXPlane)
        {
            var x = DistanceToTarget * Math.Cos(angleToMinZXPlane) * Math.Sin(angleToMinZYPlane);
            var z = DistanceToTarget * Math.Cos(angleToMinZXPlane) * Math.Cos(angleToMinZYPlane);
            var y = DistanceToTarget * Math.Sin(angleToMinZXPlane);

            return new Vector3D<float>((float)x, (float)y, (float)z);
        }
    }
}
