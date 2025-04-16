using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace DDNCadAddins.Commands.XClipTest
{
    /// <summary>
    /// 测试图层结构
    /// </summary>
    public class TestLayers
    {
        public string Layer1 { get; set; }
        public string Layer2 { get; set; }
        public string RedLayer { get; set; }
        public string BlueLayer { get; set; }
        public string GreenLayer { get; set; }
        public string YellowLayer { get; set; }
    }
    
    /// <summary>
    /// 块状态类，用于保存块的初始状态
    /// </summary>
    public class BlockState
    {
        public ObjectId BlockId { get; set; }
        public string BlockName { get; set; }
        public Point3d Position { get; set; }
        public ObjectId LayerId { get; set; }
        public Autodesk.AutoCAD.Colors.Color Color { get; set; }
        public ObjectId Linetype { get; set; }
        public double LinetypeScale { get; set; }
        public bool IsXClipped { get; set; }
        public ObjectId ParentId { get; set; }
        public double Rotation { get; set; }
        public double Scale { get; set; }
    }

    /// <summary>
    /// 测试数据类，用于保存测试环境中的所有数据
    /// </summary>
    public class TestData
    {
        public TestLayers Layers { get; set; }
        public List<ObjectId> BlockIds { get; private set; } = new List<ObjectId>();
        public Dictionary<string, ObjectId> NamedBlocks { get; private set; } = new Dictionary<string, ObjectId>();
        
        public void AddBlock(string name, ObjectId id)
        {
            if (id != ObjectId.Null && id.IsValid)
            {
                this.BlockIds.Add(id);
                if (!string.IsNullOrEmpty(name)) {NamedBlocks[name] = id;}
            }
        }
    }
} 