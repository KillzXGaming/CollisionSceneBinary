using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using IONET;
using IONET.Core;
using IONET.Core.Model;

namespace CollisionSceneBinaryTool
{
    public class CsbImporter
    {
        public class ImportSettings
        {
            //Simple collision object
            //Lacks octree search table so not ideal for complex mesh data
            public bool IsMapObject;

            public List<MeshSetting> MeshSettings = new List<MeshSetting>();

            public MeshSetting GetMeshSettings(string name)
            {
                //get import settings for target mesh
                var mesh_settings = this.MeshSettings.FirstOrDefault(x => x.Name == name);
                return mesh_settings != null ? mesh_settings : new MeshSetting("");
            }
        }

        public class MeshSetting
        {
            public string Name;
            public ulong Flags;
            public uint MaterialAttribute;

            public MeshSetting(string name)
            {
                Name = name;
            }
        }

        public class ImportResults
        {
            public CsbFile CollisionScene;
            public CtbFile CollisionTable;
        }

        public static void Import(string filePath, string name, string output_folder,
            bool is_big_endian = false, bool is_map_object = false)
        {
            Console.WriteLine($"Loading file data");

            var results = ImportFromDae(filePath, is_map_object);

            {
                var mem = new MemoryStream();
                results.CollisionScene.Save(mem, is_big_endian);
                //big endian (color splash) has no zst compression
                if (is_big_endian)
                    File.WriteAllBytes(Path.Combine(output_folder, $"{name}.csb"), mem.ToArray());
                else
                    File.WriteAllBytes(Path.Combine(output_folder, $"{name}.csb.zst"), Zstd.Compress(mem.ToArray()));
            }
            if (results.CollisionTable != null) 
            {
                var mem = new MemoryStream();
                results.CollisionTable.Save(mem, is_big_endian);
                //big endian (color splash) has no zst compression
                if (is_big_endian)
                    File.WriteAllBytes(Path.Combine(output_folder, $"{name}.ctb"), mem.ToArray());
                else
                    File.WriteAllBytes(Path.Combine(output_folder, $"{name}.ctb.zst"), Zstd.Compress(mem.ToArray()));
            }
        }

        public static ImportResults ImportFromDae(string filePath, bool is_map_object = false)
        {
            var csb = new CsbFile();
            var scene = IOManager.LoadScene(filePath, new IONET.ImportSettings());
            var settings = new ImportSettings() { IsMapObject = is_map_object };

            foreach (var mesh in scene.Models[0].Meshes)
            {
                MeshSetting meshSetting = new MeshSetting(mesh.Name);
                meshSetting.Flags = 0;
                meshSetting.MaterialAttribute = 0;

                //try to detect the material and flags via material name
                if (!string.IsNullOrEmpty(mesh.Polygons[0].MaterialName))
                {
                    var mat = mesh.Polygons[0].MaterialName;
                    var values = mat.Split("_");
                    if (values.Length == 2)
                    {
                        string mat_attr = values[0].Replace("MAT", "");
                        string flag = values[1].Replace("FLAG", "");

                        uint.TryParse(mat_attr, out meshSetting.MaterialAttribute);
                        ulong.TryParse(flag, out meshSetting.Flags);
                    }
                    else if (mat.StartsWith("MAT"))
                    {
                        string mat_attr = mat.Replace("MAT", "");
                        uint.TryParse(mat_attr, out meshSetting.MaterialAttribute);
                    }
                    else if (mat.StartsWith("FLAG"))
                    {
                        string flag = mat.Replace("FLAG", "");
                        ulong.TryParse(flag, out meshSetting.Flags);
                    }
                }

                settings.MeshSettings.Add(meshSetting);
            }
           return Import(csb, scene, settings);
        }

        public static ImportResults Import(CsbFile csb, IOScene scene, ImportSettings settings)
        {
            var iomodel = scene.Models[0];

            csb.Models.Clear();
            csb.Nodes.Clear();

            //Node tree
            List<IOMesh> meshes = iomodel.Meshes;

            Dictionary<IOMesh, int> meshToNodeID = new Dictionary<IOMesh, int>();

            Console.WriteLine($"Generating collision scene binary");

            //Always first model
            csb.Models.Add(new CsbFile.Model() { Name = "DEADBEEF" });

            //Make a node hierarchy if necessary
            if (scene.Nodes.Count == 0)
            {
                IONode root = new IONode();
                root.Name = "Collision";
                scene.Nodes.Add(root);

                IONode parent = new IONode() { Name = "Node_0" };
                root.AddChild(parent);

                int id = 0;
                while (id < iomodel.Meshes.Count)
                {
                    var mesh = iomodel.Meshes[id];

                    //reaches close to the flag limit of .csb, make a new parent
                    if (parent.Children.Length >= 100)
                    {
                        parent = new IONode();
                        parent.Name = $"Node_{root.Children.Length}";
                        root.AddChild(parent);
                    }
                    IONode n = new IONode();
                    n.Name = mesh.Name;
                    n.Mesh = mesh;
                    parent.AddChild(n);

                    id++;
                }
            }

            //models combined into one model that use a shared buffer for triangle searching
            if (settings.IsMapObject == false)
            {
                //load meshes from node tree
                meshes.Clear();

                var cModel = csb.Models[0];

                void LoadNode(IONode node)
                {
                    var cNode = new CsbFile.Node();
                    cNode.NumChildren = (byte)node.Children.Length;
                    cNode.ID = (ushort)csb.Nodes.Count;
                    csb.Nodes.Add(cNode);

                    //Load either a collision object (marked as MAPOBJ_), a mesh, or raw node
                    if (node.Name.StartsWith("MAPOBJ_"))
                    {
                        bool is_sphere = node.Name.StartsWith("MAPOBJ_SPHERE");

                        node.Name = node.Name.Replace("MAPOBJ_SPHERE_", "");
                        node.Name = node.Name.Replace("MAPOBJ_BOX_", "");

                        //map object node
                        var cObject = new CsbFile.CollisionObject();
                        cObject.Name = node.Name;
                        //2 points. It is unclear what point 2 does, but both are the same value
                        cObject.Point1 = node.Translation;
                        cObject.Point2 = node.Translation;
                        cObject.Size = node.Scale;
                        cObject.Rotation = node.RotationEuler;
                        csb.Objects.Add(cObject);

                        if (is_sphere)
                        {
                            cObject.Radius = node.Scale.X;
                            cObject.IsSphere = true;
                        }

                        cObject.NodeIndex = (uint)cNode.ID;

                        Console.WriteLine($"cObject {cObject.Name}");
                    }
                    else if (node.Mesh != null) {
                        if (!meshToNodeID.ContainsKey(node.Mesh))
                        {
                            meshes.Add(node.Mesh);
                            meshToNodeID.Add(node.Mesh, cNode.ID);

                            var iomesh = node.Mesh;
                            //get import settings for target mesh
                            var mesh_settings = settings.GetMeshSettings(iomesh.Name);

                            var cMesh = new CsbFile.Mesh();
                            cMesh.Name = iomesh.Name;
                            cModel.Meshes.Add(cMesh);

                            cMesh.ColFlag = mesh_settings.Flags;
                            cMesh.MaterialAttribute = mesh_settings.MaterialAttribute;

                            List<Triangle> triangles = new List<Triangle>();
                            List<Vector3> positions = new List<Vector3>();

                            ToTriangles(iomesh, 
                                (uint)cModel.Positions.Count, (uint)cModel.Triangles.Count, out triangles, out positions);

                            //Add positions to global buffer
                            cModel.Positions.AddRange(positions);
                            //Add triangles to global buffer
                            cModel.Triangles.AddRange(triangles);

                            cMesh.NumVertices = iomesh.Vertices.Count;
                            cMesh.NumTriangles = triangles.Count;

                            cMesh.NodeIndex = (int)cNode.ID;
                        }
                    }
                    else
                    {
                        //mesh node
                        var cMesh = new CsbFile.Mesh();
                        cMesh.Name = node.Name;
                        cModel.Meshes.Add(cMesh);

                        cMesh.NodeIndex = (int)cNode.ID;
                    }

                    foreach (IONode child in node.Children)
                        LoadNode(child);
                }

                var root_nodes = scene.Nodes.Where(x => x.Parent == null).ToList();
                if (root_nodes.Count != 1)
                {
                    //start with a root node if multiple exist
                    var cNode = new CsbFile.Node();
                    cNode.NumChildren = (byte)root_nodes.Count;
                    cNode.ID = (ushort)csb.Nodes.Count;
                    csb.Nodes.Add(cNode);

                    //mesh node
                    var cMesh = new CsbFile.Mesh();
                    cMesh.Name = "Collision";
                    cModel.Meshes.Add(cMesh);

                    cMesh.NodeIndex = (int)cNode.ID;

                    foreach (var n in root_nodes)
                        LoadNode(n);
                }
                else
                {
                    foreach (var n in root_nodes)
                        LoadNode(n);
                }

                cModel.Bounding.Compute(cModel.Positions);
                cModel.NumVertices = (uint)cModel.Positions.Count;
                cModel.NumTriangles = (uint)cModel.Triangles.Count;

                //No sub models so use defaults
                csb.SubModelBounding = new BoundingBox()
                {
                    Min = new Vector3(-999999f),
                    Max = new Vector3(999999f),
                };
                //Generate a collision table
                CtbFile ctbFile = new CtbFile();
                ctbFile.Generate(csb);

                return new ImportResults()
                {
                    CollisionScene = csb,
                    CollisionTable = ctbFile,
                };
            }
            else  //Meshes split into individual models (lacks triangle octree searching)
            {
                foreach (var iomesh in meshes)
                {
                    //get import settings for target mesh
                    var mesh_settings = settings.GetMeshSettings(iomesh.Name);

                    var cModel = new CsbFile.Model();
                    cModel.Name = iomesh.Name;
                    csb.Models.Add(cModel);

                    cModel.ColFlag = mesh_settings.Flags;
                    cModel.MaterialAttribute = mesh_settings.MaterialAttribute;

                    List<Triangle> triangles = new List<Triangle>();
                    List<Vector3> positions = new List<Vector3>();

                    ToTriangles(iomesh, (uint)cModel.Positions.Count, (uint)cModel.Triangles.Count, out triangles, out positions);

                    cModel.Positions.AddRange(positions);
                    cModel.Triangles.AddRange(triangles);
                    cModel.NumVertices = (uint)iomesh.Vertices.Count;
                    cModel.NumTriangles = (uint)triangles.Count;
                    cModel.Bounding.Compute(cModel.Positions);

                    if (meshToNodeID.ContainsKey(iomesh))
                        cModel.NodeIndex = (uint)meshToNodeID[iomesh];
                    else
                    {
                        //Add and set node
                        cModel.NodeIndex = (uint)csb.Nodes.Count;

                        var cNode = new CsbFile.Node();
                        cNode.ID = (ushort)csb.Nodes.Count;
                        csb.Nodes.Add(cNode);
                    }
                }
                csb.SubModelBounding.Compute(csb.Models.SelectMany(x => x.Positions).ToList());

                return new ImportResults()
                {
                    CollisionScene = csb,
                    CollisionTable = null,
                };
            }
        }

        //Generates the triangle and position lists with optimizations. Start indices needed for combined buffer type
        static void ToTriangles(IOMesh mesh, uint start_pos_idx, uint start_tri_idx, out List<Triangle> out_triangles, out List<Vector3> out_positions)
        {
            HashSet<string> faceHashes = new HashSet<string>();

            List<Triangle> triangles = new List<Triangle>();
            List<Vector3> positions = new List<Vector3>();

            Dictionary<string, int> positionTable = new Dictionary<string, int>();

            foreach (var poly in mesh.Polygons)
            {
                for (int i = 0; i < poly.Indicies.Count; i += 3)
                {
                    var triangle = new Triangle();
                    triangle.Vertices = new Vector3[3];
                    triangle.ID = (int)start_tri_idx + triangles.Count;

                    for (int j = 0; j < 3; j++)
                    {
                        var pos = mesh.Vertices[poly.Indicies[i + j]].Position;
                        triangle.Vertices[j] = pos;

                        if (j == 0) triangle.A = start_pos_idx + (uint)IndexOfVertex(pos, positions, positionTable);
                        if (j == 1) triangle.B = start_pos_idx + (uint)IndexOfVertex(pos, positions, positionTable);
                        if (j == 2) triangle.C = start_pos_idx + (uint)IndexOfVertex(pos, positions, positionTable);
                    }

                    Vector3 direction = Vector3.Cross(
                        triangle.Vertices[1] - triangle.Vertices[0],
                        triangle.Vertices[2] - triangle.Vertices[0]);

                    if ((direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z) < 0.01) continue;
                    direction = Vector3.Normalize(direction);

                    triangle.Normal = direction;

                    string str = $"{triangle.Vertices[0]}{triangle.Vertices[1]}{triangle.Vertices[2]}";
                    if (!faceHashes.Contains(str))
                    {
                        faceHashes.Add(str);
                        triangles.Add(triangle);
                    }
                }
            }
            out_triangles = triangles;
            out_positions = positions;
        }

        static int IndexOfVertex(Vector3 value, List<Vector3> valueList, Dictionary<string, int> lookupTable)
        {
            string key = value.ToString();
            if (!lookupTable.ContainsKey(key))
            {
                valueList.Add(value);
                lookupTable.Add(key, lookupTable.Count);
            }

            return lookupTable[key];
        }
    }
}
