using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CollisionSceneBinaryTool
{
    public class Triangle
    {
        public Vector3[] Vertices = new Vector3[3];

        public Vector3 Normal = new Vector3(0, 1, 0);

        public uint A;
        public uint B;
        public uint C;

        public int ID;
    }
}
