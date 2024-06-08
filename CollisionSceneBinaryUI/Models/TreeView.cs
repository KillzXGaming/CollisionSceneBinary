using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data;
using Avalonia.Controls.Selection;
using CollisionSceneBinaryTool;
using static CollisionSceneBinaryTool.CsbFile;

namespace CollisionSceneBinaryUI.Models
{
    public class TreeView : ObservableObject
    {
        public ObservableCollection<TreeNode> _nodes = new ObservableCollection<TreeNode>();

        public ObservableCollection<TreeNode> Nodes
        {
            get { return _nodes; }
            set { SetProperty(ref _nodes, value); }
        }


        private TreeNode _selectedNode;
        public TreeNode SelectedNode
        {
            get { return _selectedNode; }
            set { SetProperty(ref _selectedNode, value); }
        }


        public ObservableCollection<TreeNode> SelectedNodes = new ObservableCollection<TreeNode>();

        public HierarchicalTreeDataGridSource<TreeNode> Source { get; }

        public Action OnSelectionChanged;

        public TreeView()
        {
            Source = new HierarchicalTreeDataGridSource<TreeNode>(_nodes)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<TreeNode>(
                        new TextColumn<TreeNode, string>("Name", x => x.Name),
                            x => x.Children, 
                            null, x => x.IsExpanded),
                        new TextColumn<TreeNode, string>("Material", x => x.Attribute),
                },
            };

            Source.RowSelection!.SelectionChanged += (sender, e) =>
            {
                var selected = ((TreeDataGridRowSelectionModel<TreeNode>)Source.Selection).SelectedItems;

                SelectedNodes.Clear();
                foreach (var n in selected)
                    if (n != null)
                        SelectedNodes.Add(n);

                SelectedNode = SelectedNodes.FirstOrDefault();
                OnSelectionChanged?.Invoke();
            }; 
            Source.RowSelection!.SingleSelect = false;
        }
    }

    public class TreeNode : ObservableObject
    {
        private string _name;

        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        private bool _expanded = true;

        public bool IsExpanded
        {
            get { return _expanded; }
            set { SetProperty(ref _expanded, value); }
        }

        private bool _selected = true;

        public bool IsSelected
        {
            get { return _selected; }
            set { SetProperty(ref _selected, value); }
        }

        public ObservableCollection<TreeNode> _children = new ObservableCollection<TreeNode>();

        public ObservableCollection<TreeNode> Children
        {
            get { return _children; }
            set { SetProperty(ref _children, value); }
        }


        private string _attribute = "";

        public string Attribute
        {
            get { return _attribute; }
            set { SetProperty(ref _attribute, value); }
        }

        private int _materialID = 0;

        public int AttributeID
        {
            get { return _materialID; }
            set { 
                SetProperty(ref _materialID, value);

                string name = ((CsbFile.MatAttributeTTYD)_materialID).ToString();
                SetProperty(ref _attribute, name);
            }
        }


        private FlagHandler _flags = new FlagHandler(0);

        public FlagHandler Flags
        {
            get { return _flags; }
            set { SetProperty(ref _flags, value); }
        }

        public TreeNode() { }

        public TreeNode(string name) { this.Name = name; }
    }
}
