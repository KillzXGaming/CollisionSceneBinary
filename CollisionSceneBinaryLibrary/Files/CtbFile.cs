using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CollisionSceneBinaryTool
{
    public class CtbFile
    {
        public uint num_model_groups = 1;
        public float root_size;
        public float unk = 1; //always 1
        public Vector3 root_position;

        public class Node
        {
            public Vector3 position;
            public float size;
            public uint node_id = 0;
            public byte child_bits = 0; //bit per octree child, total of 8
            public byte root_flag = 0x7F;
            public ushort padding;
            public uint num_triangles;

            public uint[] TriangleIndices;

            public List<Node> Children = new List<Node>();
        }

        public List<Node> Nodes = new List<Node>();

        public CtbFile() { }

        public CtbFile(Stream stream, bool big_endian = false)
        {
            Read(new BinaryDataReader(stream), big_endian);
        }

        public void Save(string path, bool big_endian = false)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write)) {
                Save(fs, big_endian);
            }
        }

        public void Save(Stream stream, bool big_endian = false)
        {
            Write(new BinaryDataWriter(stream), big_endian);
        }

        private void Read(BinaryDataReader reader, bool big_endian = false)
        {
            if (big_endian)
                reader.ByteOrder = ByteOrder.BigEndian;

            reader.BaseStream.Position = 0;

            reader.ReadUInt32(); //0
            reader.ReadUInt32(); //0
            reader.ReadUInt32(); //0
            num_model_groups = reader.ReadUInt32(); //1

            root_size = reader.ReadSingle();
            unk = reader.ReadSingle(); //1
            root_position.X = reader.ReadSingle();
            root_position.Y = reader.ReadSingle();
            root_position.Z = reader.ReadSingle();

            uint num_nodes = reader.ReadUInt32();
            uint num_root_triangles = reader.ReadUInt32();

            for (int i = 0; i < num_nodes; i++)
                Nodes.Add(new Node()
                {
                    position = new Vector3(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle()),
                    size = reader.ReadSingle(),
                    node_id = reader.ReadUInt32(),
                    child_bits = reader.ReadByte(),
                    root_flag = reader.ReadByte(),
                    padding = reader.ReadUInt16(),
                    num_triangles = reader.ReadUInt32(),
                });

            //Connect children if possible
            int index = 0;

            void LoadNode(Node n)
            {
                index++;

                if (n.child_bits == 0)
                    return;

                //8 possible children
                for (int j = 0; j < 8; j++)
                {
                    if ((n.child_bits >> j & 1) != 0)
                    {
                        n.Children.Add(Nodes[index]);
                        LoadNode(Nodes[index]);
                    }
                }
            }

            LoadNode(Nodes.FirstOrDefault());

            for (int i = 0; i < num_nodes; i++)
            {
                Nodes[i].TriangleIndices = new uint[Nodes[i].num_triangles];
                for (int j = 0; j < Nodes[i].num_triangles; j++)
                    Nodes[i].TriangleIndices[j] = reader.ReadUInt32();
            }
        }

        private void Write(BinaryDataWriter writer, bool big_endian = false)
        {
            if (big_endian)
                writer.ByteOrder = ByteOrder.BigEndian;

            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(num_model_groups);
            writer.Write(Nodes[0].size);
            writer.Write(unk);
            writer.Write(Nodes[0].position.X);
            writer.Write(Nodes[0].position.Y);
            writer.Write(Nodes[0].position.Z);
            writer.Write(this.Nodes.Count);
            writer.Write(this.Nodes[0].TriangleIndices.Length);
            for (int i = 0; i < Nodes.Count; i++)
            {
                writer.Write(Nodes[i].position.X);
                writer.Write(Nodes[i].position.Y);
                writer.Write(Nodes[i].position.Z);
                writer.Write(Nodes[i].size);
                writer.Write(Nodes[i].node_id);
                writer.Write(Nodes[i].child_bits);
                writer.Write(Nodes[i].root_flag);
                writer.Write(Nodes[i].padding);
                writer.Write(Nodes[i].TriangleIndices.Length);
            }
            for (int i = 0; i < Nodes.Count; i++)
            {
                for (int j = 0; j < Nodes[i].TriangleIndices.Length; j++)
                    writer.Write(Nodes[i].TriangleIndices[j]);
            }
        }

        public void Generate(CsbFile csbFile)
        {
            Console.WriteLine($"Generating collision table binary");

            //CTB only gets used for singular csb model files
            var model = csbFile.Models.FirstOrDefault();
            //No triangles to search for, skip
            if (model.Triangles.Count == 0)
                return;

            Vector3 min = model.Bounding.Min;
            Vector3 max = model.Bounding.Max;
            Vector3 size = (max - min);
            Vector3 center = (min + max) / 2f;

            float scale = Math.Max(max.X - min.X, max.Z - min.Z);
            //largest scale halved, but slightly scaled up
            //Unsure how this is really handled. 
            var root_scale = scale * 0.68f;
            var root_position = new Vector3(center.X, 0, center.Z);

            var octree = OctreeGenerator.Generate(root_position, root_scale, model.Triangles);

            Node root = new Node() { root_flag = 1 };
            Nodes.Clear();
            Nodes.Add(root);

            root.TriangleIndices = GetTriangles(octree).ToArray();
            root.position = root_position;
            root.size = root_scale;
            root.node_id = 0;

            int id = 1;
            SetupOctree(octree, root, ref id);
        }

        private void SetupOctree(OctreeGenerator.OctreeNode node, Node cTreeNode, ref int id)
        {
            for (int i = 0; i < node.Children?.Length; i++)
            {
                if (node.Children[i] == null)
                    continue;

                var triangles = GetTriangles(node.Children[i]);
                if (triangles.Count == 0)
                    continue;

                cTreeNode.child_bits |= (byte)(1 << i);

                Node c = new Node();
                cTreeNode.Children.Add(c);
                Nodes.Add(c);

                c.position = node.Children[i].Position;
                c.size = node.Children[i].Scale;
                c.node_id = (uint)i;
                c.root_flag = 0x7F;
                if (node.Children[i].IsLeaf)
                    c.node_id = (uint)(id + i);

                c.TriangleIndices = triangles.ToArray();
                SetupOctree(node.Children[i], c, ref id);
            }
            id += 8;
        }

        private List<uint> GetTriangles(OctreeGenerator.OctreeNode node)
        {
            if (node == null) return new List<uint>();

            List<uint> indices = new List<uint>();
            indices.AddRange(node.Triangles.Select(x => (uint)x.ID));

            for (int i = 0; i < node.Children?.Length; i++)
                indices.AddRange(GetTriangles(node.Children[i]));

            return indices.Distinct().OrderBy(x => x).ToList();
        }
    }
}
