using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CollisionSceneBinaryTool
{
    public class BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;

        public void Read(BinaryReader reader)
        {
            Min = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Max = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Min);
            writer.Write(Max);
        }

        public void Compute(List<Vector3> positions)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;

            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;

            for (int i = 0; i < positions.Count; i++)
            {
                minX = MathF.Min(minX, positions[i].X);
                minY = MathF.Min(minY, positions[i].Y);
                minZ = MathF.Min(minZ, positions[i].Z);
                maxX = MathF.Max(maxX, positions[i].X);
                maxY = MathF.Max(maxY, positions[i].Y);
                maxZ = MathF.Max(maxZ, positions[i].Z);
            }
            Min = new Vector3(minX, minY, minZ);
            Max = new Vector3(maxX, maxY, maxZ);
        }
    }
}
