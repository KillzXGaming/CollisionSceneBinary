using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Text;
using static CollisionSceneBinaryTool.CsbFile;

namespace CollisionSceneBinaryTool
{
    public class CsbFile
    {
        public List<Model> Models = new List<Model>();
        public List<Node> Nodes = new List<Node>();
        public List<CollisionObject> Objects = new List<CollisionObject>();

        private uint Unknown = 1;
        private ushort Unknown3 = 2;

        private byte Unknown4 = 0xFE;
        private byte Unknown5 = 0x7;

        public BoundingBox SubModelBounding = new BoundingBox(); //min max vec3

        public class CollisionObject
        {
            public float Unknown;
            public Vector3 Point1;
            public Vector3 Point2;
            public Vector3 Size = Vector3.One;
            public Vector3 Rotation;

            public float Radius = 0.7f; //if IsSphere = true

            public float[] BoxExtra = new float[] { 0, 0, 1, 0, 0, 0, 0, 1, 0 };

            public bool IsSphere = false;

            public ulong ColFlag;

            public uint NodeIndex;
            public string Name { get; set; }
        }

        public class Node
        {
            public ushort ID;
            public byte Flags;
            public byte NumChildren;
        }

        public class Model
        {
            public uint NumTriangles;
            public uint NumVertices;

            public uint Unknown0 = 3;
            public ulong ColFlag;
            public uint Unknown2;
            public uint MaterialAttribute;
            public uint Unknown4;
            public uint Unknown5 = 1;

            public Vector3 Zero;
            public Vector3 Translate;
            public Vector3 Rotation;

            public uint NodeIndex;

            public BoundingBox Bounding = new BoundingBox(); //min max vec3

            //Default name when meshes are combined into one model buffer
            public string Name = "DEADBEEF";

            public List<Mesh> Meshes = new List<Mesh>();

            public List<Triangle> Triangles = new List<Triangle>();
            public List<Vector3> Positions = new List<Vector3>();

        }

        public class Mesh
        {
            public string Name;
            public uint MaterialAttribute;
            public ulong ColFlag;

            public int NodeIndex;

            public int NumVertices;
            public int NumTriangles;

            public List<Vector3> Positions = new List<Vector3>();
            public List<Triangle> Triangles = new List<Triangle>();

            public Mesh() { }

            public Mesh(string name)
            {
                Name = name;
            }
        }

        public CsbFile() { }

        public CsbFile(Stream stream, bool bigEndian = false)
        {
            Read(new BinaryDataReader(stream), bigEndian);
        }

        public void Save(string path, bool bigEndian = false)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                Save(fs, bigEndian);
            }
        }

        public void Save(Stream stream, bool bigEndian = false)
        {
            Write(new BinaryDataWriter(stream), bigEndian);
        }

        private void Read(BinaryDataReader reader, bool bigEndian)
        {
            if (bigEndian)
                reader.ByteOrder = ByteOrder.BigEndian;

            bool isVersion1 = bigEndian;

            //optional objects that form a sphere or box
            //these are generally used to trigger things
            uint num_sphere_objects = reader.ReadUInt32();

            CollisionObject[] sphere_objects = new CollisionObject[num_sphere_objects];
            for (int i = 0; i < num_sphere_objects; i++)
            {
                sphere_objects[i] = new CollisionObject();
                sphere_objects[i].Unknown = reader.ReadSingle(); //always 0
                sphere_objects[i].Point1 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                sphere_objects[i].Point2 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                sphere_objects[i].Radius = reader.ReadSingle();
                sphere_objects[i].IsSphere = true;
            }

            uint num_box_objects = reader.ReadUInt32();

            CollisionObject[] box_objects = new CollisionObject[num_box_objects];
            for (int i = 0; i < num_box_objects; i++)
            {
                box_objects[i] = new CollisionObject();
                box_objects[i].Unknown = reader.ReadSingle(); //always 0
                box_objects[i].Point1 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                box_objects[i].Point2 = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                box_objects[i].Size = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                box_objects[i].Rotation = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                box_objects[i].BoxExtra = reader.ReadSingles(9); //Always 0,0,1,0,0,0,0,1,0 
            }

            uint unk = reader.ReadUInt32(); //0 (maybe another object type). Never used in game

            //add both scene groups
            Objects.AddRange(sphere_objects);
            Objects.AddRange(box_objects);

            Unknown = reader.ReadUInt32(); //unk (always 1)
            reader.ReadBytes(16); //0

            uint[] object_name_offsets = reader.ReadUInt32s((int)Objects.Count);
            ulong[] object_flags = new ulong[Objects.Count];

            for (int i = 0; i < Objects.Count; i++)
            {
                if (isVersion1)
                    object_flags[i] = reader.ReadUInt32();
                else
                    object_flags[i] = reader.ReadUInt64();
            }

            ushort[] object_indices = reader.ReadUInt16s((int)Objects.Count);

            uint object_string_table_length = reader.ReadUInt32();
            var object_string_table_pos = reader.BaseStream.Position;
            reader.ReadBytes((int)object_string_table_length);

            for (int i = 0; i < Objects.Count; i++)
            {
                Objects[i].ColFlag = object_flags[i];
                Objects[i].NodeIndex = object_indices[i];

                using (reader.BaseStream.TemporarySeek
                    (object_string_table_pos + object_name_offsets[i], SeekOrigin.Begin))
                {
                    Objects[i].Name = reader.ReadZeroTerminatedString();
                }
            }

            Unknown3 = reader.ReadUInt16(); //1 for CS, 2 for other games (version??)
            ushort num_models = reader.ReadUInt16();
            uint num_meshes = reader.ReadUInt32();
            uint[] name_offsets = reader.ReadUInt32s((int)num_meshes);
            uint[] tri_offsets = reader.ReadUInt32s((int)num_meshes);
            uint[] vert_offsets = reader.ReadUInt32s((int)num_meshes);

            ulong[] flag_array = new ulong[(int)num_meshes];
            for (int i = 0; i < num_meshes; i++)
            {
                if (isVersion1)
                    flag_array[i] = reader.ReadUInt32();
                else
                    flag_array[i] = reader.ReadUInt64();
            }
            uint[] attributes = reader.ReadUInt32s((int)num_meshes);
            uint[] model_ids = reader.ReadUInt32s((int)num_meshes);

            ushort[] mesh_node_ids = reader.ReadUInt16s((int)num_meshes);
            //string table
            uint string_table_length = reader.ReadUInt32();

            long string_offset = reader.BaseStream.Position;
            reader.ReadBytes((int)string_table_length);

            ushort num_nodes = reader.ReadUInt16();
            for (int i = 0; i < num_nodes; i++)
            {
                Nodes.Add(new Node()
                {
                    ID = reader.ReadUInt16(), //id
                    Flags = reader.ReadByte(), //flag,
                    NumChildren = reader.ReadByte(),
                });
            }

            Mesh[] meshes = new Mesh[num_meshes];
            for (int i = 0; i < num_meshes; i++)
            {
                meshes[i] = new Mesh();
                meshes[i].MaterialAttribute = attributes[i];
                meshes[i].NodeIndex = mesh_node_ids[i];
                meshes[i].ColFlag = flag_array[i];

                using (reader.BaseStream.TemporarySeek(string_offset + name_offsets[i], SeekOrigin.Begin))
                {
                    meshes[i].Name = reader.ReadZeroTerminatedString();
                }
            }

            //model group headers
            Model[] models = new Model[num_models];
            for (int i = 0; i < num_models; i++)
            {
                models[i] = new Model();

                models[i].Unknown0 = reader.ReadUInt32(); //3
                reader.ReadUInt32(); //id always 0
                if (!isVersion1)
                    reader.ReadUInt64(); //0

                reader.ReadUInt32(); //0
                reader.ReadUInt32(); //0

                models[i].Name = Encoding.UTF8.GetString(reader.ReadBytes(64)).Replace("\0", "");
                models[i].Unknown5 = reader.ReadUInt32();
                models[i].NumVertices = reader.ReadUInt32();
                models[i].NumTriangles = reader.ReadUInt32();
                models[i].Zero = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                models[i].Translate = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                models[i].Rotation = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            Models.AddRange(models);

            for (int i = 0; i < num_models; i++)
            {
                models[i].Bounding.Read(reader);

                Vector3[] positions = new Vector3[models[i].NumVertices];

                for (int v = 0; v < models[i].NumVertices; v++)
                {
                    positions[v] = new Vector3(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }

                Triangle[] tris = new Triangle[models[i].NumTriangles];
                for (int v = 0; v < models[i].NumTriangles; v++)
                {
                    tris[v] = new Triangle() { ID = v };
                    tris[v].A = reader.ReadUInt32();
                    tris[v].B = reader.ReadUInt32();
                    tris[v].C = reader.ReadUInt32();
                    tris[v].Normal = new Vector3(
                         reader.ReadSingle(),
                         reader.ReadSingle(),
                         reader.ReadSingle());

                    tris[v].Vertices[0] = positions[tris[v].A];
                    tris[v].Vertices[1] = positions[tris[v].B];
                    tris[v].Vertices[2] = positions[tris[v].C];
                }

                models[i].Positions.AddRange(positions);
                models[i].Triangles.AddRange(tris);

                //DEADBEEF model has combined meshes
                //Typically paired with a .ctb file for searching collision with octrees

                for (int m = 0; m < meshes.Length; m++)
                {
                    //num of vertices and tris is next index - current
                    var tri_idx = tri_offsets[m];
                    var vtx_idx = vert_offsets[m];

                    var tri_end_idx = models[i].NumTriangles - 1;
                    var vtx_end_idx = models[i].NumVertices - 1;

                    if (m < meshes.Length - 1 && tri_offsets[m + 1] != models[i].NumTriangles)
                        tri_end_idx = tri_offsets[m + 1];

                    if (m < meshes.Length - 1 && vert_offsets[m + 1] != models[i].NumVertices)
                        vtx_end_idx = vert_offsets[m + 1];

                    if (tri_idx >= tri_end_idx || models[i].NumTriangles == 0)
                        continue;

                    var num_verts = vtx_end_idx - vtx_idx;
                    var num_tris = tri_end_idx - tri_idx;

                    meshes[m].NumVertices = (int)num_verts;
                    meshes[m].NumTriangles = (int)num_tris;

                    meshes[m].Triangles.AddRange(tris.ToList().GetRange((int)tri_idx, (int)num_tris));
                }
                models[i].Meshes.AddRange(meshes);
            }

            reader.ReadUInt32(); //0
            Unknown4 = reader.ReadByte();
            Unknown5 = reader.ReadByte();
            reader.ReadUInt16(); //0

            uint num_split_models = reader.ReadUInt32();
            SubModelBounding.Read(reader);

            Model[] models_split = new Model[num_split_models];
            for (int i = 0; i < num_split_models; i++)
            {
                models_split[i] = new Model();

                models_split[i].Unknown0 = reader.ReadUInt32(); //Always 3
                models_split[i].NodeIndex = reader.ReadUInt16(); //id
                reader.ReadUInt16(); //0

                if (isVersion1)
                {
                    models_split[i].ColFlag = reader.ReadUInt32();
                    models_split[i].MaterialAttribute = reader.ReadUInt32();
                }
                else
                {
                    models_split[i].ColFlag = reader.ReadUInt64();
                    models_split[i].MaterialAttribute = reader.ReadUInt32();
                    models_split[i].Unknown4 = reader.ReadUInt32(); //Always 0
                }

                models_split[i].Name = Encoding.UTF8.GetString(reader.ReadBytes(64)).Replace("\0", "");
                models_split[i].Unknown5 = reader.ReadUInt32(); //always 1
                models_split[i].NumVertices = reader.ReadUInt32();
                models_split[i].NumTriangles = reader.ReadUInt32();
                models_split[i].Zero = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                models_split[i].Translate = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                models_split[i].Rotation = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                models_split[i].Bounding.Read(reader);

                Vector3[] positions = new Vector3[models_split[i].NumVertices];

                for (int v = 0; v < models_split[i].NumVertices; v++)
                {
                    positions[v] = new Vector3(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }

                Triangle[] tris = new Triangle[models_split[i].NumTriangles];
                for (int v = 0; v < models_split[i].NumTriangles; v++)
                {
                    tris[v] = new Triangle() { ID = v };
                    tris[v].A = reader.ReadUInt32();
                    tris[v].B = reader.ReadUInt32();
                    tris[v].C = reader.ReadUInt32();
                    tris[v].Normal = new Vector3(
                         reader.ReadSingle(),
                         reader.ReadSingle(),
                         reader.ReadSingle());

                    tris[v].Vertices[0] = positions[tris[v].A];
                    tris[v].Vertices[1] = positions[tris[v].B];
                    tris[v].Vertices[2] = positions[tris[v].C];
                }

                models_split[i].Positions.AddRange(positions);
                models_split[i].Triangles.AddRange(tris);
            }
            Models.AddRange(models_split);
        }

        private void Write(BinaryDataWriter writer, bool bigEndian)
        {
            if (bigEndian)
                writer.ByteOrder = ByteOrder.BigEndian;

            bool isVersion1 = bigEndian;

            var sphere_objects = Objects.Where(x => x.IsSphere).ToList();
            var box_objects = Objects.Where(x => !x.IsSphere).ToList();

            writer.Write(sphere_objects.Count);
            foreach (var group in sphere_objects)
            {
                writer.Write(group.Unknown);
                writer.Write(group.Point1);
                writer.Write(group.Point2);
                writer.Write(group.Radius);
            }

            writer.Write(box_objects.Count);

            foreach (var group in box_objects)
            {
                writer.Write(group.Unknown);
                writer.Write(group.Point1);
                writer.Write(group.Point2);
                writer.Write(group.Size);
                writer.Write(group.Rotation);
                writer.Write(group.BoxExtra);
            }
            writer.Write(0); //0 (maybe another object type

            writer.Write(Unknown); //unk (always 1)
            writer.Write(0); //0
            writer.Write(0); //0
            writer.Write(0); //0
            writer.Write(0); //0

            //object name offsets
            int object_name_offset = 0;
            foreach (var group in Objects)
            {
                writer.Write(object_name_offset);
                object_name_offset += group.Name.Length + 1;
            }

            foreach (var group in Objects)
            {
                if (isVersion1)
                    writer.Write((uint)group.ColFlag);
                else
                    writer.Write((ulong)group.ColFlag);
            }

            foreach (var group in Objects)
                writer.Write((ushort)group.NodeIndex);

            var string_table = BuildStringTable(Objects.Select(x => x.Name).ToList());
            writer.Write(string_table.Length);
            writer.Write(string_table);

            //Only select the first model 
            //Additional models don't use a combined buffer
            var meshes = this.Models[0].Meshes.ToList();

            writer.Write((ushort)Unknown3); //unk
            writer.Write((ushort)1); //1
            writer.Write(meshes.Count); //mesh count
            //name offsets
            int name_offset = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                writer.Write(name_offset);
                name_offset += meshes[i].Name.Length + 1;
            }
            //triangle start indices
            int tri_index = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                writer.Write(tri_index);
                tri_index += meshes[i].NumTriangles;
            }
            //vertex start indices
            int vtx_index = 0;
            for (int i = 0; i < meshes.Count; i++)
            {
                writer.Write(vtx_index);
                vtx_index += meshes[i].NumVertices;
            }
            if (isVersion1)
            {
                //uint32 flags
                for (int i = 0; i < meshes.Count; i++)
                    writer.Write((uint)meshes[i].ColFlag);
            }
            else
            {
                //uint64 flags
                for (int i = 0; i < meshes.Count; i++)
                    writer.Write(meshes[i].ColFlag);
            }
            //uint32 material attributes
            for (int i = 0; i < meshes.Count; i++)
                writer.Write(meshes[i].MaterialAttribute);
            //model indices
            for (int i = 0; i < meshes.Count; i++)
                writer.Write(0);
            //node indices
            for (int i = 0; i < meshes.Count; i++)
                writer.Write((ushort)meshes[i].NodeIndex);
            //build string table
            var mesh_string_table = BuildStringTable(meshes.Select(x => x.Name).ToList());
            writer.Write(mesh_string_table.Length);
            writer.Write(mesh_string_table);
            //nodes
            writer.Write((ushort)this.Nodes.Count);
            for (int i = 0; i < Nodes.Count; i++)
            {
                writer.Write(Nodes[i].ID);
                writer.Write(Nodes[i].Flags);
                writer.Write(Nodes[i].NumChildren);
            }
            foreach (var model in Models)
            {
                writer.Write(model.Unknown0);  //3
                writer.Write((ushort)model.NodeIndex);
                writer.Write((ushort)0);

                if (isVersion1)
                {
                    //uint32 flags
                    writer.Write((uint)model.ColFlag);
                    writer.Write(model.MaterialAttribute);
                }
                else
                {
                    //uint64 flags
                    writer.Write(model.ColFlag);
                    writer.Write(model.MaterialAttribute);
                    writer.Write(model.Unknown4);
                }
                writer.WriteFixedString(model.Name, 64);
                writer.Write(model.Unknown5);
                writer.Write(model.Positions.Count);
                writer.Write(model.Triangles.Count);
                writer.Write(model.Zero);
                writer.Write(model.Translate);
                writer.Write(model.Rotation);
                model.Bounding.Write(writer);
                foreach (var pos in model.Positions)
                    writer.Write(pos);
                foreach (var tri in model.Triangles)
                {
                    writer.Write(tri.A);
                    writer.Write(tri.B);
                    writer.Write(tri.C);
                    writer.Write(tri.Normal);
                }
                if (model.Name == "DEADBEEF") //DEADBEEF model where it has model list and total bounding of sub models
                {
                    writer.Write(0);
                    writer.Write(Unknown4);
                    writer.Write(Unknown5);
                    writer.Write((ushort)0);

                    //sub models
                    writer.Write(Models.Count - 1);
                    SubModelBounding.Write(writer);
                }
            }
        }

        private byte[] BuildStringTable(List<string> list)
        {
            var mem = new MemoryStream();
            using (var writer = new BinaryWriter(mem))
            {
                foreach (var str in list)
                {
                    writer.Write(Encoding.UTF8.GetBytes(str));
                    writer.Write((byte)0); //zero terimate
                }
                writer.AlignBytes(4);
            }
            return mem.ToArray();
        }

        public enum MatAttributeORIGAMI_KING
        {
            COL_MATERIAL_PLAIN,
            COL_MATERIAL_TURF,
            COL_MATERIAL_GRASS,
            COL_MATERIAL_GRASS2,
            COL_MATERIAL_STONE,
            COL_MATERIAL_WOOD,
            COL_MATERIAL_WOODPLATE,
            COL_MATERIAL_METAL,
            COL_MATERIAL_METALPLATE,
            COL_MATERIAL_SAND,
            COL_MATERIAL_WATER,
            COL_MATERIAL_ICE,
            COL_MATERIAL_SNOW,
            COL_MATERIAL_CLOTHTHICK,
            COL_MATERIAL_GRAVEL,
            COL_MATERIAL_CONCRETE,
            COL_MATERIAL_FLOWER,
            COL_MATERIAL_LEAF,
            COL_MATERIAL_SOIL,
            COL_MATERIAL_GLASS,
            COL_MATERIAL_PAPERTAPE,
            COL_MATERIAL_HOLE,
            COL_MATERIAL_TATAMI,
            COL_MATERIAL_POTTERY,
            COL_MATERIAL_LAVA,
            COL_MATERIAL_REALSOIL,
            COL_MATERIAL_REALICE,
            COL_MATERIAL_REALWATER,
            COL_MATERIAL_OIL,
            COL_MATERIAL_WATERSHALLOW,
            COL_MATERIAL_TREE,
            COL_MATERIAL_TREESLIM,
            COL_MATERIAL_PAMPASGRASS,
            COL_MATERIAL_SQUIDINK,
            COL_MATERIAL_TREEBIG,
            COL_MATERIAL_BUSH,
            COL_MATERIAL_WIRE,
            COL_MATERIAL_STONEPROCESS,
            COL_MATERIAL_CLOTH,
            COL_MATERIAL_GOLD,
            COL_MATERIAL_MUD,
            COL_MATERIAL_THORN,
            COL_MATERIAL_MAGICCIRCLE,
            COL_MATERIAL_SOILHARD,
            COL_MATERIAL_PLASTIC,
            COL_MATERIAL_CARDBOARD,
            COL_MATERIAL_METALCHAIN,
            COL_MATERIAL_CLOTHHARD,
            COL_MATERIAL_CLOTHSOFT,
            COL_MATERIAL_PAPER,
            COL_MATERIAL_PAPERTHICK,
            COL_MATERIAL_POTTERYBIG,
            COL_MATERIAL_RUBBER,
            COL_MATERIAL_METALFENCE,
            COL_MATERIAL_HIGHGRASS,
            COL_MATERIAL_BRICK,
            COL_MATERIAL_GENERICHARD,
            COL_MATERIAL_GENERICSOFT,
            COL_MATERIAL_JUNGLEGRASS,
            COL_MATERIAL_ASH,
            COL_MATERIAL_ROOFINGTILE,
            COL_MATERIAL_SPECIALUSAGE1,
            COL_MATERIAL_SPECIALUSAGE2,
            COL_MATERIAL_SPECIALUSAGE3,
            COL_MATERIAL_SPECIALUSAGE4,
            COL_MATERIAL_REALOIL,
            COL_MATERIAL_ROPE,
            COL_MATERIAL_MAGICCIRCLEICE,
            COL_MATERIAL_MOMIJI,
            COL_MATERIAL_VINYL,
            COL_MATERIAL_LEAFFALLEN,
            COL_MATERIAL_TOMEIBLOCKMARK,
            COL_MATERIAL_BAMBOO,
            COL_MATERIAL_STRAW,
            COL_MATERIAL_ROOFINGTILEWOOD,
            COL_MATERIAL_METALCAN,
            COL_MATERIAL_METALCANSMALL,
            COL_MATERIAL_CLOTHEXTRATHICK,
            COL_MATERIAL_WETLAND,
            COL_MATERIAL_WATERRUNNING,
            COL_MATERIAL_HIGHGRASS2,
            COL_MATERIAL_MARIOOBJECT,
            COL_MATERIAL_GLASSBROKEN,
            COL_MATERIAL_ICENOSLIP,
            COL_MATERIAL_STONEPLATE,
            COL_MATERIAL_METALHOLLOW,
            COL_MATERIAL_METALHOLLOWBIG,
            COL_MATERIAL_METALHOLLOWSMALL,
            COL_MATERIAL_METALPIPE,
            COL_MATERIAL_METALPIPEBIG,
            COL_MATERIAL_METALPIPESMALL,
            COL_MATERIAL_WOODRATTLE,
            COL_MATERIAL_STONERATTLE,
            COL_MATERIAL_CLOTHTHICKHARD,
            COL_MATERIAL_LEAFFALLENWATER,
            COL_MATERIAL_CLOTHEXTRATHICKHARD
        }

        [Flags]
        public enum COL_FLAG_ORIGAMI_KING : ulong
        {
            COL_FLAG_NONE = 0,
            COL_FLAG_IGNORE_PAPER = 1 << 0,
            COL_FLAG_IGNORE_TOMEI = 1 << 1,
            COL_FLAG_IGNORE_HAMMER = 1 << 2,
            COL_FLAG_IGNORE_ENEMY = 1 << 3,
            COL_FLAG_IGNORE_PARTY = 1 << 4,
            COL_FLAG_IGNORE_BOTHSIDES = 1 << 5,
            COL_FLAG_IGNORE_OTOTO = 1 << 6,
            COL_FLAG_IGNORE_TOGE = 1 << 7,
            COL_FLAG_IGNORE_HAMMERPAPER = 1 << 8,
            COL_FLAG_IGNORE_MARIOHAMMER = 1 << 9,
            COL_FLAG_IGNORE_STRONG = 1 << 10,
            COL_FLAG_IGNORE_MARIOPAPER = 1 << 11,
            COL_FLAG_IGNORE_SWINGHAMMER = 1 << 12,
            COL_FLAG_IGNORE_SLOPE = 1 << 13,
            COL_FLAG_IGNORE_WEAK = 1 << 14,
            COL_FLAG_IGNORE_EMERGENCY = 1 << 15,
            COL_FLAG_IGNORE_X_INKMASK = 1 << 16,
            COL_FLAG_IGNORE_IGNOREHANDLE = 1 << 17,
            COL_FLAG_IGNORE_NOTONMOVE = 1 << 18,
            COL_FLAG_IGNORE_IGNOREITEM = 1 << 19,
            COL_FLAG_IGNORE_ITEM = 1 << 20,
            COL_FLAG_IGNORE_KAKURE = 1 << 21,
            COL_FLAG_IGNORE_SOFT = 1 << 22,
            COL_FLAG_IGNORE_WALL = 1 << 23,
            COL_FLAG_IGNORE_OTOTOGUARD = 1 << 24,
            COL_FLAG_IGNORE_HEADBUTTEFFECT = 1 << 25,
            COL_FLAG_IGNORE_IGNORENPC = 1 << 26,
            COL_FLAG_IGNORE_X_ONLYPAINT = 1 << 27,
            COL_FLAG_IGNORE_IGNOREPAPER = 1 << 28,
            COL_FLAG_IGNORE_X_INVISIBLE = 1 << 29,
            COL_FLAG_IGNORE_X_NOTPAINT = 1 << 30,
            COL_FLAG_IGNORE_WATERSURFACE = 1UL << 31,
            COL_FLAG_IGNORE_VEHICLE = 1UL << 32,
            COL_FLAG_IGNORE_HARIKO = 1UL << 33,
            COL_FLAG_IGNORE_VEHICLEFORCEHIT = 1UL << 34,
            COL_FLAG_IGNORE_SPECIALMESH = 1UL << 35,
            COL_FLAG_IGNORE_PRESIMPLIFY = 1UL << 36,
            COL_FLAG_IGNORE_OUTLINE = 1UL << 37,
            COL_FLAG_IGNORE_ENEMYITEM = 1UL << 38,
            COL_FLAG_IGNORE_IGNOREHARIKO = 1UL << 39,
            COL_FLAG_IGNORE_NORECOVERPOS = 1UL << 40,
            COL_FLAG_IGNORE_NOGENERATEHOLE = 1UL << 41,
            COL_FLAG_IGNORE_HINT = 1UL << 42,
            COL_FLAG_IGNORE_ONDYNAMIC_NO_IGNOREROTATE = 1UL << 43,
            COL_FLAG_IGNORE_NOCAMERASHAKE = 1UL << 44,
            COL_FLAG_IGNORE_WEAPON = 1UL << 45,
            COL_FLAG_IGNORE_NOPUSHUPWALL = 1UL << 46,
            COL_FLAG_IGNORE_ICEPLATE = 1UL << 47,
            COL_FLAG_IGNORE_HOLESURFACE = 1UL << 48,
            COL_FLAG_IGNORE_HOLEWALL = 1UL << 49,
            COL_FLAG_IGNORE_HITONDASHATTACK = 1UL << 50,
            COL_FLAG_IGNORE_HOLEINNERWALL = 1UL << 51,
            COL_FLAG_IGNORE_HOLEGUARDWALL = 1UL << 52,
            COL_FLAG_IGNORE_HOLEBORDER = 1UL << 53,
            COL_FLAG_IGNORE_VEHICLEMESH = 1UL << 54,
            COL_FLAG_IGNORE_HARIKOMESH = 1UL << 55,
            COL_FLAG_IGNORE_HOLEWALLCOVER = 1UL << 56,
            COL_FLAG_IGNORE_ITEMMESH = 1UL << 57,
            COL_FLAG_IGNORE_PLAYER_MASK = 1UL << 58,
            COL_FLAG_IGNORE_HAMMER_MASK = 1UL << 59,
            COL_FLAG_IGNORE_PAPER_MASK = 1UL << 60,
            COL_FLAG_IGNORE_NPC_MASK = 1UL << 61,
            COL_FLAG_IGNORE_NPCWEAPON_MASK = 1UL << 62,
            COL_FLAG_IGNORE_HARIKO_MASK = 1UL << 63,
            COL_FLAG_IGNORE_PARTY_MASK = 1UL << 64,
            /*   COL_FLAG_IGNORE_ITEM_MASK = 1UL << 65,
               COL_FLAG_IGNORE_HINT_MASK = 1UL << 66,
               COL_FLAG_IGNORE_PLAYER_VEHICLE_MASK = 1UL << 67,
               COL_FLAG_IGNORE_VEHICLE_WHEEL_MASK = 1UL << 68,
               COL_FLAG_IGNORE_UNCOLLECTABLEPAPER_MASK = 1UL << 69,
               COL_FLAG_IGNORE_HOLE_SURFACE_MASK = 1UL << 70,
               COL_FLAG_AUGOGEN_MASK = 1UL << 71,
               COL_FLAG_IGNORE_SLANTING = 1UL << 72,*/
        }

        public enum MatAttributeTTYD
        {
            COL_MATERIAL_PLAIN,
            COL_MATERIAL_TURF,
            COL_MATERIAL_GRASS,
            COL_MATERIAL_GRASS2,
            COL_MATERIAL_STONE,
            COL_MATERIAL_WOOD,
            COL_MATERIAL_WOODPLATE,
            COL_MATERIAL_METAL,
            COL_MATERIAL_METALPLATE,
            COL_MATERIAL_SAND,
            COL_MATERIAL_WATER,
            COL_MATERIAL_ICE,
            COL_MATERIAL_SNOW,
            COL_MATERIAL_CLOTHTHICK,
            COL_MATERIAL_GRAVEL,
            COL_MATERIAL_CONCRETETUBE,
            COL_MATERIAL_FLOWER,
            COL_MATERIAL_LEAF,
            COL_MATERIAL_SOIL,
            COL_MATERIAL_GLASS,
            COL_MATERIAL_PAPERTAPE,
            COL_MATERIAL_HOLE,
            COL_MATERIAL_TATAMI,
            COL_MATERIAL_POTTERY,
            COL_MATERIAL_LAVA,
            COL_MATERIAL_REALSOIL,
            COL_MATERIAL_REALICE,
            COL_MATERIAL_REALWATER,
            COL_MATERIAL_OIL,
            COL_MATERIAL_WATERSHALLOW,
            COL_MATERIAL_TREE,
            COL_MATERIAL_TREESLIM,
            COL_MATERIAL_PAMPASGRASS,
            COL_MATERIAL_SQUIDINK,
            COL_MATERIAL_TREEBIG,
            COL_MATERIAL_BUSH,
            COL_MATERIAL_WIRE,
            COL_MATERIAL_STONEPROCESS,
            COL_MATERIAL_CLOTH,
            COL_MATERIAL_GOLD,
            COL_MATERIAL_MUD,
            COL_MATERIAL_THORN,
            COL_MATERIAL_MAGICCIRCLE,
            COL_MATERIAL_SOILHARD,
            COL_MATERIAL_PLASTIC,
            COL_MATERIAL_CARDBOARD,
            COL_MATERIAL_METALCHAIN,
            COL_MATERIAL_CLOTHHARD,
            COL_MATERIAL_CLOTHSOFT,
            COL_MATERIAL_PAPER,
            COL_MATERIAL_PAPERTHICK,
            COL_MATERIAL_POTTERYBIG,
            COL_MATERIAL_RUBBER,
            COL_MATERIAL_METALFENCE,
            COL_MATERIAL_HIGHGRASS,
            COL_MATERIAL_BRICK,
            COL_MATERIAL_GENERICHARD,
            COL_MATERIAL_GENERICSOFT,
            COL_MATERIAL_JUNGLEGRASS,
            COL_MATERIAL_ASH,
            COL_MATERIAL_ROOFINGTILE,
            COL_MATERIAL_SPECIALUSAGE1,
            COL_MATERIAL_SPECIALUSAGE2,
            COL_MATERIAL_SPECIALUSAGE3,
            COL_MATERIAL_SPECIALUSAGE4,
            COL_MATERIAL_REALOIL,
            COL_MATERIAL_ROPE,
            COL_MATERIAL_MAGICCIRCLEICE,
            COL_MATERIAL_MOMIJI,
            COL_MATERIAL_VINYL,
            COL_MATERIAL_LEAFFALLEN,
            COL_MATERIAL_REPLICATE,
            COL_MATERIAL_BAMBOO,
            COL_MATERIAL_STRAW,
            COL_MATERIAL_ROOFINGTILEWOOD,
            COL_MATERIAL_METALCAN,
            COL_MATERIAL_METALCANSMALL,
            COL_MATERIAL_CLOTHEXTRATHICK,
            COL_MATERIAL_WETLAND,
            COL_MATERIAL_WATERRUNNING,
            COL_MATERIAL_HIGHGRASS2,
            COL_MATERIAL_MARIOOBJECT,
            COL_MATERIAL_GLASSBROKEN,
            COL_MATERIAL_ICENOSLIP,
            COL_MATERIAL_STONEPLATE,
            COL_MATERIAL_METALHOLLOW,
            COL_MATERIAL_METALHOLLOWBIG,
            COL_MATERIAL_METALHOLLOWSMALL,
            COL_MATERIAL_METALPIPE,
            COL_MATERIAL_METALPIPEBIG,
            COL_MATERIAL_METALPIPESMALL,
            COL_MATERIAL_WOODRATTLE,
            COL_MATERIAL_STONERATTLE,
            COL_MATERIAL_CLOTHTHICKHARD,
            COL_MATERIAL_LEAFFALLENWATER,
            COL_MATERIAL_CLOTHEXTRATHICKHARD,
            COL_MATERIAL_POTTERYRATTLE,
            COL_MATERIAL_PLANT,
            COL_MATERIAL_BRICKRATTLE,
            COL_MATERIAL_WATERDEEP,
            COL_MATERIAL_SOILSOFT,
            COL_MATERIAL_METALPLATETHIN,
            COL_MATERIAL_WATERSOAP,
            COL_MATERIAL_FLOWERCARPET,
            COL_MATERIAL_ASPARAGUS,
            COL_MATERIAL_VINEWONDER,
            COL_MATERIAL_HOLOGRAM,
            COL_MATERIAL_MELON,
            COL_MATERIAL_CONCRETEPLATE,
            COL_MATERIAL_SOILLITTLESOFT,
            COL_MATERIAL_TILE,
            COL_MATERIAL_CONCRETE,
            COL_MATERIAL_GOLDCOIN,
            COL_MATERIAL_LIQUIDSTICKY,
            COL_MATERIAL_ANOTHERDIMENSION,

            //Not defined in code
            COL_MATERIAL_WATERPUDDLE = 122,
        }

        public enum COL_FLAGS_TTYD : ulong
        {
            COL_FLAG_TYPE_1 = 1 << 0,
            COL_FLAG_TOMEI = 1 << 1,
            COL_FLAG_HAMMER = 1 << 2,
            COL_FLAG_ENEMY = 1 << 3,
            COL_FLAG_PARTY = 1 << 4,
            COL_FLAG_TYPE_2 = 1 << 5,
            COL_FLAG_TYPE_3 = 1 << 6,
            COL_FLAG_TYPE_4 = 1 << 7,
            COL_FLAG_SLIT = 1 << 8,
            COL_FLAG_ROLL = 1 << 9,
            COL_FLAG_OUTLINE = 1 << 10,
            COL_FLAG_TYPE_5 = 1 << 11,
            COL_FLAG_TYPE_6 = 1 << 12,
            COL_FLAG_TYPE_7 = 1 << 13,
            COL_FLAG_TYPE_8 = 1 << 14,
            COL_FLAG_EMERGENCY = 1 << 15,
            COL_FLAG_TYPE_9 = 1 << 16,
            COL_FLAG_TYPE_10 = 1 << 17,
            COL_FLAG_TYPE_11 = 1 << 18,
            COL_FLAG_TYPE_12 = 1 << 19,
            COL_FLAG_ITEM = 1 << 20,
            COL_FLAG_TYPE_13 = 1 << 21,
            COL_FLAG_TYPE_14 = 1 << 22,
            COL_FLAG_TYPE_15 = 1 << 23,
            COL_FLAG_TYPE_16 = 1 << 24,
            COL_FLAG_TYPE_17 = 1 << 25,
            COL_FLAG_IGNORE_NPC = 1UL << 26,
            COL_FLAG_TYPE_18 = 1 << 27,
            COL_FLAG_TYPE_19 = 1 << 28,
            COL_FLAG_TYPE_20 = 1 << 29,
            COL_FLAG_IGNORE_SHIP = 1UL << 30,
            COL_FLAG_TYPE_21 = 1UL << 31,
            COL_FLAG_TYPE_22 = 1UL << 32,
            COL_FLAG_TYPE_23 = 1UL << 33,
            COL_FLAG_IGNORE_TOMEI = 1UL << 34,
            COL_FLAG_IGNORE_KAKURE = 1UL << 35,
            COL_FLAG_TYPE_24 = 1UL << 36,
            COL_FLAG_IGNORE_ENEMY = 1UL << 37,
            COL_FLAG_PARTYBIG = 1UL << 38,
            COL_FLAG_TYPE_25 = 1UL << 39,
            COL_FLAG_TYPE_26 = 1UL << 40,
            COL_FLAG_TYPE_27 = 1UL << 41,
            COL_FLAG_TYPE_28 = 1UL << 42,
            COL_FLAG_TYPE_29 = 1UL << 43,
            COL_FLAG_TYPE_30 = 1UL << 44,
            COL_FLAG_TYPE_31 = 1UL << 45,
            COL_FLAG_TYPE_32 = 1UL << 46,
            COL_FLAG_TYPE_33 = 1UL << 47,
            COL_FLAG_IGNORE_PLAYER = 1UL << 48,
            COL_FLAG_IGNORE_PARTY = 1UL << 49,
            COL_FLAG_IGNORE_SLIT = 1UL << 50,
            COL_FLAG_IGNORE_ROLL = 1UL << 51,
            COL_FLAG_TYPE_34 = 1UL << 52,
            COL_FLAG_ABYSS = 1UL << 53,
            COL_FLAG_TYPE_35 = 1UL << 54,
            COL_FLAG_IGNORE_JABARA = 1UL << 55,
            COL_FLAG_TYPE_36 = 1UL << 56,
            COL_FLAG_TYPE_37 = 1UL << 57,
            COL_FLAG_TYPE_38 = 1UL << 58,
            COL_FLAG_TYPE_39 = 1UL << 59,
            COL_FLAG_TYPE_40 = 1UL << 60,
            COL_FLAG_IGNORE_PARTY_MASK = 1UL << 61,
            COL_FLAG_TYPE_41 = 1UL << 62,
            COL_FLAG_TYPE_42 = 1UL << 63
        }

        public enum COL_FLAGS_COLOR_SPLASH : uint
        {
            COL_FLAG_TYPE_0 = 1 << 0,
            COL_FLAG_TOMEI = 1 << 1,
            COL_FLAG_TYPE_2 = 1 << 2,
            COL_FLAG_TYPE_3 = 1 << 3,
            COL_FLAG_TYPE_4 = 1 << 4,
            COL_FLAG_TYPE_5 = 1 << 5,
            COL_FLAG_TYPE_6 = 1 << 6,
            COL_FLAG_TYPE_7 = 1 << 7,
            COL_FLAG_TYPE_8 = 1 << 8,
            COL_FLAG_TYPE_9 = 1 << 9,
            COL_FLAG_TYPE_10 = 1 << 10,
            COL_FLAG_TYPE_11 = 1 << 11,
            COL_FLAG_TYPE_12 = 1 << 12,
            COL_FLAG_TYPE_13 = 1 << 13,
            COL_FLAG_TYPE_14 = 1 << 14,
            COL_FLAG_TYPE_15 = 1 << 15,
            COL_FLAG_TYPE_16 = 1 << 16,
            COL_FLAG_TYPE_17 = 1 << 17,
            COL_FLAG_TYPE_18 = 1 << 18,
            COL_FLAG_TYPE_19 = 1 << 19,
            COL_FLAG_TYPE_20 = 1 << 20,
            COL_FLAG_TYPE_21 = 1 << 21,
            COL_FLAG_TYPE_22 = 1 << 22,
            COL_FLAG_TYPE_23 = 1 << 23,
            COL_FLAG_TYPE_24 = 1 << 24,
            COL_FLAG_TYPE_25 = 1 << 25,
            COL_FLAG_TYPE_26 = 1 << 26,
            COL_FLAG_TYPE_27 = 1 << 27,
            COL_FLAG_TYPE_28 = 1 << 28,
            COL_FLAG_TYPE_29 = 1 << 29,
            COL_FLAG_TYPE_30 = 1 << 30,
        }
    }
}
