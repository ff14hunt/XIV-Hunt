using System;

namespace XIVDB
{

    public class EnemyObject
    {
        public ushort Id { get; set; }
        public Map_Data Map_data { get; set; }
    }

    public class Map_Data
    {
        public Point[] Points { get; set; }
    }

    public class AppPosition
    {
        public Position Position { get; set; }
    }

    public class Point
    {
        public ushort Map_id { get; set; }
        public ushort Placename_id { get; set; }
        public AppPosition App_position { get; set; }
        public AppData App_data { get; set; }
    }

    public class Position
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class AppData
    {
        public Fate Fate { get; set; }
        //public int Hp { get; set; }
        //public int Level { get; set; }
        //public int Mp { get; set; }
    }

    public class Fate
    {
        public uint fate { get; set; }
        public bool Is_fate { get; set; }
    }
}
