using IONET;
using IONET.Collada.B_Rep.Surfaces;
using IONET.Collada.Core.Geometry;
using IONET.Collada.FX.Materials;
using IONET.Core;
using IONET.Core.Model;
using IONET.Core.Skeleton;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static CollisionSceneBinaryTool.CsbFile;

namespace CollisionSceneBinaryTool
{
    public class CsbExporter
    {
        public static void Export(CsbFile csb, string filePath)
        {
            IOScene scene = new IOScene();

            IOModel iomodel = new IOModel();
            scene.Models.Add(iomodel);

            IOMaterial SetupMaterial(uint attribute, ulong flag)
            {
                IOMaterial material = new IOMaterial();
                material.Name = $"MAT{attribute}_FLAG{flag}";
                if (!scene.Materials.Any(x => x.Name == material.Name))
                    scene.Materials.Add(material);

                return material;
            }

            //Node tree

            int currentIdx = 0;

            List<IONode> node_list = new List<IONode>();

            void LoadNode(CsbFile.Node node, IOBone parent = null)
            {
                currentIdx++;

                int index = csb.Nodes.IndexOf(node);

                //find the model or mesh that links to this to label it
                var model = csb.Models.FirstOrDefault(x => x.NodeIndex == index);

                string name = $"";
                if (csb.Models[0].Meshes.Count > 0)
                {
                    var mesh = csb.Models[0].Meshes.FirstOrDefault(x => x.NodeIndex == index);
                    if (mesh != null)
                        name = $"{mesh.Name}";
                }

                IONode bone = new IONode();
                bone.Name = name;
                bone.Scale = new Vector3(1);
                bone.Translation = new Vector3(0);
                bone.RotationEuler = new Vector3(0);
                node_list.Add(bone);

                for (int i = 0; i < node.NumChildren; i++)
                    LoadNode(csb.Nodes[currentIdx], bone);

                if (parent == null)
                    scene.Nodes.Add(bone);
                else
                    parent.AddChild(bone);
            }
            LoadNode(csb.Nodes[0]);

            foreach (var obj in csb.Objects)
            {
                string type = obj.IsSphere ? "MAPOBJ_SPHERE" : "MAPOBJ_BOX";

                IOMaterial material = SetupMaterial(0, obj.ColFlag);

                //Add meshes as map objects
                IOMesh iomesh = SetupMesh($"{type}_{obj.Name}", material.Name, new List<Triangle>());
                iomodel.Meshes.Add(iomesh);

                node_list[(int)obj.NodeIndex].Name = iomesh.Name;
                node_list[(int)obj.NodeIndex].Translation = obj.Point1;
                node_list[(int)obj.NodeIndex].RotationEuler = obj.Rotation;
                node_list[(int)obj.NodeIndex].Scale = obj.Size;

                if (obj.IsSphere)
                    node_list[(int)obj.NodeIndex].Scale = new Vector3(obj.Radius);


                node_list[(int)obj.NodeIndex].Mesh = iomesh;
            }

            foreach (var model in csb.Models)
            {
                if (model.Meshes.Count > 0)
                {
                    foreach (var mesh in model.Meshes)
                    {
                        IOMaterial material = SetupMaterial(mesh.MaterialAttribute, mesh.ColFlag);

                        IOMesh iomesh = SetupMesh(mesh.Name, material.Name, mesh.Triangles);
                        if (iomesh.Vertices.Count > 0)
                            iomodel.Meshes.Add(iomesh);

                        if (iomesh.Vertices.Count > 0)
                            node_list[mesh.NodeIndex].Mesh = iomesh;
                    }
                }
                else if (model.Triangles.Count > 0) 
                {
                    IOMaterial material = SetupMaterial(model.MaterialAttribute, model.ColFlag);

                    IOMesh iomesh = SetupMesh(model.Name, material.Name, model.Triangles);
                    if (iomesh.Vertices.Count > 0)
                        iomodel.Meshes.Add(iomesh);

                    if (iomesh.Vertices.Count > 0)
                    {
                        node_list[(int)model.NodeIndex].Mesh = iomesh;
                        node_list[(int)model.NodeIndex].Translation = model.Translate;
                        node_list[(int)model.NodeIndex].RotationEuler = model.Rotation;
                        node_list[(int)model.NodeIndex].Scale = new Vector3(1);
                    }
                }
            }

            IOManager.ExportScene(scene, filePath, new ExportSettings());
        }

        static IOMesh SetupMesh(string name, string material, List<Triangle> triangles)
        {
            IOMesh mesh = new IOMesh();
            mesh.Name = name;

            int faceIdx = 0;

            IOPolygon poly = new IOPolygon();
            mesh.Polygons.Add(poly);

            poly.MaterialName = material;

            foreach (var tri in triangles)
            {
                mesh.Vertices.Add(new IOVertex() { Position = tri.Vertices[0], Normal = tri.Normal, });
                mesh.Vertices.Add(new IOVertex() { Position = tri.Vertices[1], Normal = tri.Normal, });
                mesh.Vertices.Add(new IOVertex() { Position = tri.Vertices[2], Normal = tri.Normal, });

                poly.Indicies.Add(faceIdx++);
                poly.Indicies.Add(faceIdx++);
                poly.Indicies.Add(faceIdx++);
            }
            return mesh;
        }
    }
}
