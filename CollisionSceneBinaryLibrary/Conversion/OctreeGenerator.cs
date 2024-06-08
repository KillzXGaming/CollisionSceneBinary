using IONET.Collada.Core.Geometry;
using IONET.Collada.Core.Transform;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CollisionSceneBinaryTool
{
    public class OctreeGenerator
    {
        public class OctreeNode
        {
            public Vector3 Position { get; private set; }
            public float Scale { get; private set; }

            public List<Triangle> Triangles;

            public OctreeNode[] Children;

            public bool IsLeaf => Children == null;

            public OctreeNode(Vector3 position, float scale)
            {
                this.Position = position;
                this.Scale = scale;
                Triangles = new List<Triangle>();
            }


            // Subdivide the node into eight children
            public void Subdivide(int depth)
            {
                //8 octrees can be loaded, but we only load 4 for now
                //Other 4 are used for height, but generally only 4 ever need to be used
                Children = new OctreeNode[4];

                float childScale = Scale / 2f;
                for (int i = 0; i < Children.Length; i++)
                {
                    Vector3 childPosition = Position + GetChildOffset(i) * childScale;
                    Children[i] = new OctreeNode(childPosition, childScale);
                }
            }

            private Vector3 GetChildOffset(int index)
            {
                return new Vector3(
                    ((index & 1) == 0 ? -1 : 1),
                    0, //ignore Y for now
                    ((index & 2) == 0 ? -1 : 1)
                );
            }
        }

        public static OctreeNode Generate(Vector3 root_position, float root_scale, List<Triangle> triangles)
        {
            Octree octree = new Octree(root_position, root_scale);
            octree.Build(triangles);
            return octree.root;
        }

        public class Octree
        {
            public OctreeNode root;
            private int maxTrianglesPerNode;
            private int maxDepth;

            public Octree(Vector3 root_position, float root_scale, int maxTrianglesPerNode = 10, int maxDepth = 6)
            {
                root = new OctreeNode(root_position, root_scale);
                this.maxTrianglesPerNode = maxTrianglesPerNode;
                this.maxDepth = maxDepth;
            }

            public void Build(List<Triangle> triangles)
            {
                root.Subdivide(0);
                foreach (var child in root.Children)
                    InsertTriangles(child, triangles, 0);
            }

            private void InsertTriangles(OctreeNode node, List<Triangle> triangles, int depth)
            {
                // Go through all triangles and remember them if they overlap with the region of this cube.
                List<Triangle> containedTriangles = new List<Triangle>();
                foreach (var triangle in triangles)
                {
                    if (TriangleHelper.TriangleCubeOverlap(triangle, node.Position, node.Scale))
                        containedTriangles.Add(triangle);
                }

                //Console.WriteLine($"{new string('-', depth)} {node.Position} scale {node.Scale} {containedTriangles.Count}");

                if (containedTriangles.Count > maxTrianglesPerNode && depth < maxDepth)
                {
                    depth++;

                    node.Subdivide(depth);
                    foreach (var child in node.Children)
                        this.InsertTriangles(child, triangles, depth);
                }
                else
                {
                    node.Triangles.AddRange(containedTriangles);
                }
            }
        }
    }
}
