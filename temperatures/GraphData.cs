using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace temperatures
{
    public class GraphData
    {
        public int Yaxis { get; set; } = 0;
        public int Xaxis { get; set; } = 0;

        public int[] PointArray { get; set; }

        public Color LineColor { get; set; }

        public int LineSize { get; set; }

        public bool NewGraph { get; set; } = true;

        //default constructor
        public GraphData() { }

        // constructor overload 1
        public GraphData(int yaxis, int xaxis, Color lineColor, int lineSize, bool newGraph)
        {
            Yaxis = yaxis;
            Xaxis = xaxis;
            PointArray = new int[1000];
            this.LineColor = lineColor;
            this.LineSize = lineSize;
            this.NewGraph = newGraph;
        }
    }
}
