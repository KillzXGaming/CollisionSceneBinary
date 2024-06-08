using CollisionSceneBinaryTool;
using CollisionSceneBinaryUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Linq;
using static CollisionSceneBinaryTool.CsbFile;

namespace CollisionSceneBinaryUI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public TreeView _treeView = new TreeView();

        public TreeView Tree
        {
            get { return _treeView; }
            set { SetProperty(ref _treeView, value); }
        }

        public ObservableCollection<string> _attributes = new ObservableCollection<string>();

        public ObservableCollection<string> Attributes
        {
            get { return _attributes; }
            set { SetProperty(ref _attributes, value); }
        }

        public ObservableCollection<string> _collisionFlags = new ObservableCollection<string>();

        public ObservableCollection<string> CollisionFlags
        {
            get { return _collisionFlags; }
            set { SetProperty(ref _collisionFlags, value); }
        }


        public ObservableCollection<string> _flagProperties = new ObservableCollection<string>();

        public ObservableCollection<string> FlagProperties
        {
            get { return _flagProperties; }
            set { SetProperty(ref _flagProperties, value); }
        }

        public ActiveGame SelectedGame = ActiveGame.PMTTYD;

        private string _selectedCollisionFlag;

        public string SelectedCollisionFlag
        {
            get { return _selectedCollisionFlag; }
            set
            {
                UpdateColFlag(value);
                SetProperty(ref _selectedCollisionFlag, "");
            }
        }

        private string _attributeName;

        public string AttributeName
        {
            get { return _attributeName; }
            set { 
                SetProperty(ref _attributeName, value);
                UpdateProperties();
            }
        }


        private int _attributeID;

        public int AttributeID
        {
            get { return _attributeID; }
            set { 
                SetProperty(ref _attributeID, value);

                string name = ((CsbFile.MatAttributeTTYD)_attributeID).ToString();
                SetProperty(ref _attributeName, name);
            }
        }

        private ulong _flags;

        public ulong Flags
        {
            get { return _flags; }
            set { SetProperty(ref _flags, value); }
        }

        public string FileName;

        private CtbFile CollisionTable;
        private CsbFile CollisionScene;

        private List<TreeNode> nodes = new List<TreeNode>();

        public MainWindowViewModel()
        {
            Tree.OnSelectionChanged += delegate
            {
                var node = Tree.SelectedNode;
                if (node != null)
                {
                    this.AttributeID = node.AttributeID;
                    this.FlagProperties.Clear();
                    foreach (var f in node.Flags.Properties)
                        this.FlagProperties.Add(this.CollisionFlags[f]);
                }
                else
                {
                    this.AttributeID = 0;
                    this.FlagProperties.Clear();

                }
            };
        }

        public void ExportScene(string path)
        {
            if (CollisionScene != null)
                CsbExporter.Export(CollisionScene, path);
        }

        public void SaveScene(string path)
        {
            OnSave();

            bool as_big_endian = this.SelectedGame == MainWindowViewModel.ActiveGame.ColorSplash;

            string folder = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);

            if (CollisionScene != null)
            {
                var mem = new MemoryStream();
                CollisionScene.Save(mem, as_big_endian);
                File.WriteAllBytes(Path.Combine(folder, $"{name}.csb.zst"), Zstd.Compress(mem.ToArray()));
            }
            if (CollisionTable != null)
            {
                var mem = new MemoryStream();
                CollisionTable.Save(mem, as_big_endian);
                File.WriteAllBytes(Path.Combine(folder, $"{name}.ctb.zst"), Zstd.Compress(mem.ToArray()));
            }
        }

        public void LoadCtbFile(CtbFile file)
        {
            CollisionTable = file;
        }

        public void LoadCsbFile(CsbFile csb)
        {
            CollisionScene = csb;

            Tree.Nodes.Clear();

            //load csb node tree
            int currentIndex = 0;

            nodes.Clear();

            void LoadNode(TreeNode parent = null)
            {
                var node = csb.Nodes[currentIndex];

                TreeNode treeNode = new TreeNode($"Node{node.ID}");
                nodes.Add(treeNode);

                if (parent != null)
                    parent.Children.Add(treeNode);
                else
                    Tree.Nodes.Add(treeNode);

                currentIndex++;

                for (int i = 0; i < node.NumChildren; i++)
                    LoadNode(treeNode);
            }

            LoadNode(null);

            //Load objects to attach as tree nodes
            foreach (var obj in csb.Objects)
            {
                nodes[(int)obj.NodeIndex].Name = obj.Name;   
            }

            //Load meshes to attach as tree nodes
            foreach (var model in csb.Models)
            {
                foreach (var mesh in model.Meshes)
                {
                    nodes[(int)mesh.NodeIndex].Name = mesh.Name;
                    if (mesh.NumVertices > 0)
                    {
                        nodes[(int)mesh.NodeIndex].AttributeID = (int)mesh.MaterialAttribute;

                        var field = mesh.GetType().GetField("ColFlag");
                        nodes[(int)mesh.NodeIndex].Flags = new FlagHandler(mesh.ColFlag);
                    }
                }

                //map objs put meshes per model
                if (csb.Models.Count > 1 && model.NodeIndex != 0)
                    nodes[(int)model.NodeIndex].Name = model.Name;
            }
            ReloadGame();
        }

        public void OnSave()
        {
            if (CollisionScene == null)
                return;

            //node order same as csb file
            //apply all UI flags and attributes
            foreach (var model in CollisionScene.Models)
            {
                foreach (var mesh in model.Meshes)
                {
                    nodes[(int)mesh.NodeIndex].Name = mesh.Name;
                    if (mesh.NumVertices > 0)
                    {
                        mesh.ColFlag = nodes[(int)mesh.NodeIndex].Flags.ToFlag();
                        mesh.MaterialAttribute = (uint)nodes[(int)mesh.NodeIndex].AttributeID;
                    }
                }

                //map objs put meshes per model
                if (CollisionScene.Models.Count > 1 && model.NodeIndex != 0)
                    model.ColFlag = nodes[(int)model.NodeIndex].Flags.ToFlag();
            }
        }

        public void UpdateProperties()
        {
            if (Tree.SelectedNodes.Count == 0)
                return;

            this.AttributeID = (int)Enum.Parse(typeof(CsbFile.MatAttributeTTYD), AttributeName);
            foreach (var node in Tree.SelectedNodes) {
                node.AttributeID = this.AttributeID;
            }
        }

        public void UpdateColFlag(string selected_flag)
        {
            if (Tree.SelectedNodes.Count == 0)
                return;

            var flag = GetEnumBitPosition((COL_FLAGS_TTYD)Enum.Parse(typeof(CsbFile.COL_FLAGS_TTYD), selected_flag));
            foreach (var node in Tree.SelectedNodes)
                node.Flags.Set(flag, true);

            //Reload active flags
            this.FlagProperties.Clear();
            foreach (var f in Tree.SelectedNode.Flags.Properties)
                this.FlagProperties.Add(this.CollisionFlags[f]);
        }

        public void RemoveColFlag(string selected_flag)
        {
            if (Tree.SelectedNodes.Count == 0)
                return;

            var flag = GetEnumBitPosition((COL_FLAGS_TTYD)Enum.Parse(typeof(CsbFile.COL_FLAGS_TTYD), selected_flag));
            foreach (var node in Tree.SelectedNodes)
                node.Flags.Set(flag, false);

            //Reload active flags
            this.FlagProperties.Clear();
            foreach (var f in Tree.SelectedNode.Flags.Properties)
                this.FlagProperties.Add(this.CollisionFlags[f]);
        }

        public void ReloadGame( )
        {
            Attributes.Clear();
            CollisionFlags.Clear();

            if (SelectedGame == ActiveGame.PMTTYD)
            {
                foreach (var value in Enum.GetValues(typeof(CsbFile.MatAttributeTTYD)))
                    Attributes.Add(value.ToString());
                foreach (var value in Enum.GetValues(typeof(CsbFile.COL_FLAGS_TTYD)))
                    CollisionFlags.Add(value.ToString());
            }
            else if (SelectedGame == ActiveGame.OrigamiKing)
            {
                foreach (var value in Enum.GetValues(typeof(CsbFile.MatAttributeORIGAMI_KING)))
                    Attributes.Add(value.ToString());
                foreach (var value in Enum.GetValues(typeof(CsbFile.COL_FLAG_ORIGAMI_KING)))
                    CollisionFlags.Add(value.ToString());
            }
            else if (SelectedGame == ActiveGame.ColorSplash)
            {
                foreach (var value in Enum.GetValues(typeof(CsbFile.MatAttributeORIGAMI_KING)))
                    Attributes.Add(value.ToString());
                foreach (var value in Enum.GetValues(typeof(CsbFile.COL_FLAGS_COLOR_SPLASH)))
                    CollisionFlags.Add(value.ToString());
            }
        }

        static int GetEnumBitPosition(COL_FLAGS_TTYD flag)
        {
            ulong value = (ulong)flag;
            int position = 0;

            while (value > 1)
            {
                value >>= 1;
                position++;
            }

            return position;
        }

        public enum ActiveGame
        {
            PMTTYD,
            OrigamiKing,
            ColorSplash,
        }
    }
}
